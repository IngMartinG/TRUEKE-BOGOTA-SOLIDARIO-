using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Pruebas.Infraestructura;
using UglyToad.PdfPig;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Empresas: verificación del SuperUsuario con el certificado, miembros por invitación, campañas ligadas a publicaciones e
/// informes de impacto emitidos por un Administrador con una foto inmutable de los registros (el PDF y el CSV dan las mismas
/// cifras y no cambian si cambian los factores). Los datos de la empresa solo los ven sus miembros.
/// </summary>
public class EmpresasTests : IClassFixture<FabricaApi>
{
    private readonly AlmacenFalso _almacen = new();
    private readonly WebApplicationFactory<Program> _f;

    public EmpresasTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen))));

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-5));
    private static string NitNuevo() => DocumentosFiscales.NormalizarNit("8" + Random.Shared.Next(10_000_000, 99_999_999));

    private async Task<HttpClient> SuperAsync() => Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private async Task<string> SubirAsync(HttpClient c, string tipo)
    {
        var (contentType, datos) = tipo == "Documento" ? ("application/pdf", "%PDF-1.7 certificado"u8.ToArray()) : ("image/png", AlmacenFalso.Png());
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo, contentType, tamanoBytes = datos.Length });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var s = (await r.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
        _almacen.Subir(s.UrlArchivo, contentType, datos);
        return s.UrlArchivo;
    }

    private async Task<HttpResponseMessage> SolicitarAsync(HttpClient c, string nit, bool declaro = true)
        => await c.PostAsJsonAsync("/api/v1/empresas/mia", new
        {
            razonSocial = "Reciclar Juntos S.A.S.", nit, declaroRepresentacion = declaro, certificadoUrl = await SubirAsync(c, "Documento")
        });

    /// <summary>Empresa aprobada por el SuperUsuario, con una campaña activa. Devuelve el cliente del Admin.</summary>
    private async Task<(HttpClient Admin, Guid EmpresaId, Guid CampanaId)> EmpresaConCampanaAsync(string alias)
    {
        var admin = await Api.RegistrarAsync(_f, alias);
        var r = await SolicitarAsync(admin, NitNuevo());
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var id = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await (await SuperAsync()).PostAsync($"/api/v1/admin/empresas/{id}/aprobar", null)).StatusCode);
        var c = await admin.PostAsJsonAsync("/api/v1/empresas/mia/campanas", new { nombre = "Donatón de uniformes", inicio = Hoy, fin = Hoy.AddDays(30) });
        Assert.True(c.IsSuccessStatusCode, await c.Content.ReadAsStringAsync());
        return (admin, id, (await c.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }

    private static async Task<Guid> PublicarAsync(HttpClient c, Guid? campanaId, decimal? kg = null, string? material = null, int categoriaId = 1)
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Uniforme escolar", descripcion = "En buen estado", categoriaId, modo = "Donacion", condicion = "Usado",
            localidad = "Kennedy", pesoEstimadoKg = kg, material, campanaId
        });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Solicitud aceptada; confirma el dueño (con foto si se da) y, si <paramref name="ambas"/>, también quien recibe.</summary>
    private static async Task<Guid> EntregarAsync(HttpClient duenio, HttpClient otro, Guid pub, string? foto = null, bool ambas = true)
    {
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me sirve" });
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        var r = await duenio.PostAsJsonAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", new { fotoEvidenciaUrl = foto });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        if (ambas) (await otro.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static string TextoPdf(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return string.Join(" ", doc.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
    }

    [Fact]
    public async Task La_verifica_el_SuperUsuario_y_los_miembros_entran_con_una_invitacion_de_un_solo_uso()
    {
        var admin = await Api.RegistrarAsync(_f, "emp-admin");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.GetAsync("/api/v1/empresas/mia")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(admin, NitNuevo(), declaro: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(admin, "1012345678")).StatusCode); // cédula, no persona jurídica
        var r = await SolicitarAsync(admin, NitNuevo());
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var mia = await r.Content.ReadFromJsonAsync<JsonElement>();
        var empresaId = mia.GetProperty("id").GetGuid();
        Assert.Equal("Pendiente", mia.GetProperty("estado").GetString());
        Assert.Equal("Admin", mia.GetProperty("miRol").GetString());
        // Antes de verificarla no hay invitaciones ni campañas
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/empresas/mia/invitaciones", new { rol = "Miembro" })).StatusCode);

        // Un Cliente no entra a la administración; el SuperUsuario ve el certificado (enlace temporal) y aprueba
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/empresas")).StatusCode);
        var super = await SuperAsync();
        var pendientes = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/empresas?estado=Pendiente");
        var fila = pendientes.EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == empresaId);
        Assert.EndsWith("?sas=lectura", fila.GetProperty("certificadoUrl").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await super.PostAsync($"/api/v1/admin/empresas/{empresaId}/aprobar", null)).StatusCode);

        var inv = await (await admin.PostAsJsonAsync("/api/v1/empresas/mia/invitaciones", new { rol = "Miembro" })).Content.ReadFromJsonAsync<JsonElement>();
        var codigo = inv.GetProperty("codigo").GetString()!;
        var ana = await Api.RegistrarAsync(_f, "emp-ana");
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync("/api/v1/empresas/unirse", new { codigo = codigo[..^2] + "xx" })).StatusCode);
        var unida = await ana.PostAsJsonAsync("/api/v1/empresas/unirse", new { codigo });
        Assert.True(unida.IsSuccessStatusCode, await unida.Content.ReadAsStringAsync());
        var deAna = await unida.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Miembro", deAna.GetProperty("miRol").GetString());
        Assert.Equal(2, deAna.GetProperty("miembros").GetArrayLength());
        Assert.Equal(0, deAna.GetProperty("invitaciones").GetArrayLength()); // solo los Admin ven las invitaciones
        Assert.DoesNotContain("@", await unida.Content.ReadAsStringAsync()); // nunca correos entre usuarios

        // El código es de un solo uso, y un Miembro no administra
        var beto = await Api.RegistrarAsync(_f, "emp-beto");
        Assert.Equal(HttpStatusCode.BadRequest, (await beto.PostAsJsonAsync("/api/v1/empresas/unirse", new { codigo })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsJsonAsync("/api/v1/empresas/mia/invitaciones", new { rol = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ana.PostAsJsonAsync("/api/v1/empresas/mia/campanas",
            new { nombre = "Campaña", inicio = Hoy, fin = Hoy.AddDays(5) })).StatusCode);
        // El único Admin no puede salir ni quedarse sin Admin
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.DeleteAsync("/api/v1/empresas/mia/membresia")).StatusCode);
        var yo = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/empresas/mia")).GetProperty("miembros").EnumerateArray()
            .Single(m => m.GetProperty("soyYo").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync($"/api/v1/empresas/mia/miembros/{yo.GetProperty("id").GetGuid()}/rol",
            new { rol = "Miembro" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync("/api/v1/empresas/mia/membresia")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.GetAsync("/api/v1/empresas/mia")).StatusCode);
    }

    [Fact]
    public async Task Solo_se_liga_una_campana_activa_de_la_propia_empresa_y_solo_la_ve_el_duenio()
    {
        var (admin, _, campana) = await EmpresaConCampanaAsync("emp-camp");
        var activas = await admin.GetFromJsonAsync<JsonElement>("/api/v1/empresas/mia/campanas-activas");
        Assert.Equal(campana, activas.EnumerateArray().Single().GetProperty("id").GetGuid());

        var ajena = await Api.RegistrarAsync(_f, "emp-ajena");
        Assert.Equal(0, (await ajena.GetFromJsonAsync<JsonElement>("/api/v1/empresas/mia/campanas-activas")).GetArrayLength());
        var r = await ajena.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Uniforme", descripcion = "En buen estado", categoriaId = 1, modo = "Donacion", condicion = "Usado", localidad = "Kennedy", campanaId = campana
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        var pub = await PublicarAsync(admin, campana);
        Assert.Equal(campana, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}")).GetProperty("campanaId").GetGuid());
        Assert.Equal(JsonValueKind.Null, (await ajena.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}")).GetProperty("campanaId").ValueKind);
        // Una campaña que terminaría antes de empezar no se acepta
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/empresas/mia/campanas",
            new { nombre = "Al revés", inicio = Hoy.AddDays(3), fin = Hoy })).StatusCode);
    }

    [Fact]
    public async Task El_informe_sale_de_la_campana_el_PDF_y_el_CSV_dan_las_mismas_cifras_y_no_cambian_con_los_factores()
    {
        var (admin, empresaId, campana) = await EmpresaConCampanaAsync("emp-inf");
        var carla = await Api.RegistrarAsync(_f, "emp-carla");
        var dani = await Api.RegistrarAsync(_f, "emp-dani");
        var eva = await Api.RegistrarAsync(_f, "emp-eva");

        // Dos entregas de la campaña (una con foto), una sin campaña y una con una sola confirmación: solo cuentan las dos primeras
        var conFoto = await EntregarAsync(admin, carla, await PublicarAsync(admin, campana, kg: 2.5m, material: "Textil"), await SubirAsync(admin, "FotoChat"));
        await EntregarAsync(admin, dani, await PublicarAsync(admin, campana)); // típico de Ropa: 0,5 kg textil
        await EntregarAsync(admin, eva, await PublicarAsync(admin, null, kg: 9m, material: "Metal"));
        await EntregarAsync(admin, carla, await PublicarAsync(admin, campana, kg: 30m, material: "Metal"), ambas: false);

        var mia = await admin.GetFromJsonAsync<JsonElement>("/api/v1/empresas/mia");
        var avance = mia.GetProperty("campanas").EnumerateArray().Single();
        Assert.Equal(2, avance.GetProperty("objetos").GetInt32());
        Assert.Equal(3m, avance.GetProperty("kg").GetDecimal());

        // Lo emite un Administrador (no el Admin de la empresa) y no se aceptan días que aún no terminan
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/admin/empresas/{empresaId}/informes",
            new { desde = Hoy.AddDays(-1), hasta = Hoy })).StatusCode);
        var super = await SuperAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostAsJsonAsync($"/api/v1/admin/empresas/{empresaId}/informes",
            new { desde = Hoy, hasta = Hoy.AddDays(1) })).StatusCode);
        var r = await super.PostAsJsonAsync($"/api/v1/admin/empresas/{empresaId}/informes", new { campanaId = campana, desde = Hoy.AddDays(-1), hasta = Hoy });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var informe = await r.Content.ReadFromJsonAsync<JsonElement>();
        var informeId = informe.GetProperty("id").GetGuid();
        Assert.Equal("2.0.0", informe.GetProperty("versiones").GetString());
        Assert.Equal(64, informe.GetProperty("huella").GetString()!.Length);

        // Lo descargan los miembros; para cualquier otra cuenta no existe
        var pdf = await (await admin.GetAsync($"/api/v1/empresas/mia/informes/{informeId}/pdf")).Content.ReadAsByteArrayAsync();
        var csvResp = await admin.GetAsync($"/api/v1/empresas/mia/informes/{informeId}/csv");
        Assert.Equal("text/csv", csvResp.Content.Headers.ContentType!.MediaType);
        var csv = Encoding.UTF8.GetString(await csvResp.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        Assert.Equal(HttpStatusCode.NotFound, (await carla.GetAsync($"/api/v1/empresas/mia/informes/{informeId}/pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await carla.GetAsync($"/api/v1/empresas/mia/informes/{informeId}/csv")).StatusCode);

        // Mismas cifras: la frase del resumen del PDF contra la suma de las filas del CSV
        var filas = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(l => l.TrimEnd('\r').Split(';')).ToList();
        Assert.Equal(2, filas.Count);
        var kgCsv = filas.Sum(f => decimal.Parse(f[7], CultureInfo.InvariantCulture));
        var co2Csv = filas.Sum(f => decimal.Parse(f[10], CultureInfo.InvariantCulture));
        Assert.Equal(3m, kgCsv);
        Assert.Equal(24m, co2Csv); // (2,5 + 0,5) kg × 8
        Assert.Single(filas, f => f[12] == "Sí"); // la entrega con foto
        Assert.All(filas, f => Assert.Equal("Donatón de uniformes", f[2]));

        var texto = TextoPdf(pdf);
        var m = Regex.Match(texto, @"completaron (\d+) intercambios con (\d+) objetos entregados: ([\d.,]+) kg reutilizados y ≈ ([\d.,]+) kg");
        Assert.True(m.Success, texto[..Math.Min(texto.Length, 2000)]);
        Assert.Equal(filas.Select(f => f[0]).Distinct().Count(), int.Parse(m.Groups[1].Value, Co));
        Assert.Equal(filas.Count, int.Parse(m.Groups[2].Value, Co));
        Assert.Equal(kgCsv, decimal.Parse(m.Groups[3].Value, Co));
        Assert.Equal(co2Csv, decimal.Parse(m.Groups[4].Value, Co));
        Assert.Contains(informe.GetProperty("huella").GetString()!, texto);
        Assert.DoesNotContain("emp-", texto); // nadie identificado
        Assert.DoesNotContain("Prueba", texto);

        // Cambiar el factor del textil no reescribe el informe
        Assert.Equal(HttpStatusCode.OK, (await super.PutAsJsonAsync("/api/v1/admin/impacto/factores/Textil",
            new { kgCo2PorKg = 10m, fuente = "Estudio nuevo", version = "2.1.0" })).StatusCode);
        var csvDespues = Encoding.UTF8.GetString(await super.GetByteArrayAsync($"/api/v1/admin/empresas/informes/{informeId}/csv")).TrimStart('﻿');
        Assert.Equal(csv, csvDespues);
        Assert.Contains("y ≈ 24,00 kg", TextoPdf(await super.GetByteArrayAsync($"/api/v1/admin/empresas/informes/{informeId}/pdf")));

        // La foto de la entrega: la ven las dos partes y la administración; nadie más
        var fotos = await carla.GetFromJsonAsync<JsonElement>($"/api/v1/solicitudes/{conFoto}/evidencias");
        var foto = fotos.EnumerateArray().Single();
        Assert.False(foto.GetProperty("esMia").GetBoolean());
        Assert.Contains("/chat/", foto.GetProperty("url").GetString());
        Assert.Equal(1, (await carla.GetFromJsonAsync<JsonElement>("/api/v1/solicitudes/enviadas")).EnumerateArray()
            .Single(s => s.GetProperty("id").GetGuid() == conFoto).GetProperty("evidencias").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await eva.GetAsync($"/api/v1/solicitudes/{conFoto}/evidencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await eva.GetAsync($"/api/v1/admin/solicitudes/{conFoto}/evidencias")).StatusCode);
        Assert.Single((await super.GetFromJsonAsync<JsonElement>($"/api/v1/admin/solicitudes/{conFoto}/evidencias")).EnumerateArray());
    }
}
