using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Fundaciones verificadas: solicitud con NIT de persona jurídica y certificado privado, revisión solo del SuperUsuario (que borra
/// el certificado al decidir y queda en la auditoría), insignia en el perfil y las publicaciones, páginas públicas sin datos de
/// contacto y "Lo que necesitamos" solo para las verificadas.
/// </summary>
public class FundacionesTests : IClassFixture<FabricaApi>
{
    private readonly AlmacenFalso _almacen = new();
    private readonly WebApplicationFactory<Program> _f;

    public FundacionesTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen))));

    /// <summary>NIT distinto en cada prueba (la base en memoria se comparte), con su dígito de verificación.</summary>
    private static string NitNuevo() => DocumentosFiscales.NormalizarNit("9" + Random.Shared.Next(10_000_000, 99_999_999));

    private async Task<HttpClient> SuperAsync() => Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private async Task<string> CertificadoAsync(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "Documento", contentType = "application/pdf", tamanoBytes = 2000 });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        var s = (await r.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
        _almacen.Subir(s.UrlArchivo, "application/pdf", "%PDF-1.7 certificado camara de comercio"u8.ToArray());
        return s.UrlArchivo;
    }

    private async Task<HttpResponseMessage> SolicitarAsync(HttpClient c, string nit, string razonSocial = "Fundación Manos de Bosa", bool declaro = true,
        bool mostrarNit = true)
        => await c.PostAsJsonAsync("/api/v1/fundaciones/mia", new
        {
            razonSocial, nit, mostrarNit, declaroRepresentacion = declaro, certificadoUrl = await CertificadoAsync(c),
            descripcion = "Acompañamos a 60 niños de Bosa con refuerzo escolar y almuerzo los sábados."
        });

    /// <summary>Cuenta con la fundación ya aprobada por el SuperUsuario.</summary>
    private async Task<(SesionMinDto Ses, HttpClient C, string Nit)> FundacionAprobadaAsync(string alias)
    {
        var ses = await Api.RegistrarSesionAsync(_f, alias);
        var c = Api.ConToken(_f, ses.Token);
        var nit = NitNuevo();
        var r = await SolicitarAsync(c, nit, $"Fundación {alias}");
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await (await SuperAsync()).PostAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/aprobar", null)).StatusCode);
        return (ses, c, nit);
    }

    [Fact]
    public async Task Solicitud_valida_nit_de_persona_juridica_declaracion_y_certificado_propio()
    {
        var c = await Api.RegistrarAsync(_f, "fundaValida");
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync("/api/v1/fundaciones/mia")).StatusCode); // nunca lo pidió

        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(c, "1032456789")).StatusCode);          // cédula, no persona jurídica
        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(c, "900123456-0")).StatusCode);         // dígito de verificación errado
        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(c, NitNuevo(), declaro: false)).StatusCode);
        var ajeno = await c.PostAsJsonAsync("/api/v1/fundaciones/mia", new
        {
            razonSocial = "Fundación Ajena", nit = NitNuevo(), declaroRepresentacion = true, certificadoUrl = "https://otro.sitio/cert.pdf",
            descripcion = "Una descripción suficientemente larga para pasar."
        });
        Assert.Equal(HttpStatusCode.BadRequest, ajeno.StatusCode);                                            // el certificado debe subirse aquí

        var nit = NitNuevo();
        var r = await SolicitarAsync(c, nit.Replace("-", "", StringComparison.Ordinal)[..^1]);                // sin dígito: se calcula
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var mia = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pendiente", mia.GetProperty("estado").GetString());
        Assert.Equal(nit, mia.GetProperty("nit").GetString());
        Assert.DoesNotContain("certificado", mia.GetRawText(), StringComparison.OrdinalIgnoreCase);         // el certificado no vuelve al front

        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(c, NitNuevo())).StatusCode);          // ya hay una en revisión
        var otra = await Api.RegistrarAsync(_f, "fundaCopia");
        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(otra, nit)).StatusCode);              // mismo NIT en otra cuenta
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/v1/fundaciones/mia")).StatusCode);
    }

    [Fact]
    public async Task Sin_foto_de_perfil_no_puede_pedir_la_verificacion()
    {
        var ses = await Api.RegistrarSesionSinFotoAsync(_f, "fundaSinFoto");
        var c = Api.ConToken(_f, ses.Token);
        var r = await c.PostAsJsonAsync("/api/v1/fundaciones/mia", new
        {
            razonSocial = "Fundación Sin Foto", nit = NitNuevo(), declaroRepresentacion = true, certificadoUrl = "https://almacen.test/subidas/x.pdf",
            descripcion = "Una descripción suficientemente larga para pasar."
        });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Solo_el_superusuario_revisa_con_enlace_temporal_y_el_certificado_se_borra_al_aprobar()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "fundaRevision");
        var c = Api.ConToken(_f, ses.Token);
        Assert.Equal(HttpStatusCode.OK, (await SolicitarAsync(c, NitNuevo())).StatusCode);

        // Un administrador (no SuperUsuario) ni la ve ni la decide
        var sesAdmin = await Api.RegistrarSesionAsync(_f, "fundaModera");
        var super = await SuperAsync();
        (await super.PatchAsJsonAsync($"/api/v1/admin/usuarios/{sesAdmin.Usuario.Id}/rol", new { rol = "Administrador" })).EnsureSuccessStatusCode();
        var admin = Api.ConToken(_f, await Api.LoginAsync(_f, sesAdmin.Usuario.Correo, Api.ClaveValida));
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/admin/fundaciones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/aprobar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/admin/fundaciones")).StatusCode);

        var pendientes = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/fundaciones");
        var p = pendientes.EnumerateArray().Single(x => x.GetProperty("usuarioId").GetGuid() == ses.Usuario.Id);
        var enlace = p.GetProperty("certificadoUrl").GetString()!;
        Assert.EndsWith("?sas=lectura", enlace);                                                      // enlace temporal
        var certificado = enlace[..^"?sas=lectura".Length];
        Assert.Contains("/documentos/", certificado);                                                 // copia privada del servidor
        Assert.True(_almacen.Existe(certificado));

        Assert.Equal(HttpStatusCode.NoContent, (await super.PostAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/aprobar", null)).StatusCode);
        Assert.False(_almacen.Existe(certificado));                                                   // minimización
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/aprobar", null)).StatusCode);

        var mia = await c.GetFromJsonAsync<JsonElement>("/api/v1/fundaciones/mia");
        Assert.Equal("Aprobada", mia.GetProperty("estado").GetString());
        var aprobadas = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/fundaciones?estado=Aprobada");
        var a = aprobadas.EnumerateArray().Single(x => x.GetProperty("usuarioId").GetGuid() == ses.Usuario.Id);
        Assert.Equal(JsonValueKind.Null, a.GetProperty("certificadoUrl").ValueKind);

        var notificaciones = await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones");
        Assert.Contains("FundacionAprobada", notificaciones.GetRawText());
    }

    [Fact]
    public async Task Rechazo_con_motivo_borra_el_certificado_y_permite_volver_a_intentar()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "fundaRechazo");
        var c = Api.ConToken(_f, ses.Token);
        var nit = NitNuevo();
        Assert.Equal(HttpStatusCode.OK, (await SolicitarAsync(c, nit)).StatusCode);
        var super = await SuperAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PostAsJsonAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/rechazar", new { motivo = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await super.PostAsJsonAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/rechazar", new { motivo = "El certificado tiene más de 30 días" })).StatusCode);

        var mia = await c.GetFromJsonAsync<JsonElement>("/api/v1/fundaciones/mia");
        Assert.Equal("Rechazada", mia.GetProperty("estado").GetString());
        Assert.Equal("El certificado tiene más de 30 días", mia.GetProperty("motivo").GetString());
        Assert.DoesNotContain(_almacen.Urls(), u => u.Contains("/documentos/" + ses.Usuario.Id.ToString("N"), StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "Cuadernos" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await SolicitarAsync(c, nit)).StatusCode);                  // corrige y vuelve a pedir
        Assert.Equal("Pendiente", (await c.GetFromJsonAsync<JsonElement>("/api/v1/fundaciones/mia")).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task La_insignia_se_ve_en_el_perfil_y_en_las_publicaciones_y_se_retira_por_moderacion()
    {
        var (ses, c, _) = await FundacionAprobadaAsync("fundaInsignia");
        var pubId = await Api.CrearPublicacionAsync(c);
        var anonimo = _f.CreateClient();

        var perfil = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.Equal("Fundación fundaInsignia", perfil.GetProperty("nombreFundacion").GetString());
        var pub = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pubId}");
        Assert.Equal("Fundación fundaInsignia", pub.GetProperty("propietario").GetProperty("nombreFundacion").GetString());

        var super = await SuperAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await super.PostAsJsonAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/retirar", new { motivo = "Se disolvió" })).StatusCode);
        perfil = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.Equal(JsonValueKind.Null, perfil.GetProperty("nombreFundacion").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/v1/fundaciones/{ses.Usuario.Id}")).StatusCode);
        var mia = await c.GetFromJsonAsync<JsonElement>("/api/v1/fundaciones/mia");
        Assert.Equal("Retirada", mia.GetProperty("estado").GetString());
        Assert.Equal("Se disolvió", mia.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task Paginas_publicas_sin_sesion_con_necesidades_y_sin_datos_privados()
    {
        var (ses, c, nit) = await FundacionAprobadaAsync("fundaPublica");
        var urgente = await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "Cuadernos cuadriculados", categoriaId = 1, cantidad = 40, urgente = true });
        Assert.True(urgente.StatusCode == HttpStatusCode.Created, await urgente.Content.ReadAsStringAsync());
        (await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "Ropa de niño talla 8" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "Algo", cantidad = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "Algo", categoriaId = 99999 })).StatusCode);

        var anonimo = _f.CreateClient();
        var lista = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/fundaciones");
        var tarjeta = lista.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == ses.Usuario.Id);
        Assert.Equal(2, tarjeta.GetProperty("necesidades").GetInt32());
        Assert.Equal(1, tarjeta.GetProperty("necesidadesUrgentes").GetInt32());

        var pagina = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/fundaciones/{ses.Usuario.Id}");
        Assert.Equal("Fundación fundaPublica", pagina.GetProperty("nombre").GetString());
        Assert.Equal(nit, pagina.GetProperty("nit").GetString());                                     // eligió mostrarlo
        var necesidades = pagina.GetProperty("necesidades").EnumerateArray().ToList();
        Assert.Equal("Cuadernos cuadriculados", necesidades[0].GetProperty("texto").GetString());   // urgentes primero
        Assert.Equal(40, necesidades[0].GetProperty("cantidad").GetInt32());
        var json = pagina.GetRawText();
        Assert.DoesNotContain(ses.Usuario.Correo, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certificado", json, StringComparison.OrdinalIgnoreCase);

        // Ocultar el NIT
        (await c.PutAsJsonAsync("/api/v1/fundaciones/mia", new { descripcion = "Refuerzo escolar y almuerzo para niños de Bosa.", mostrarNit = false })).EnsureSuccessStatusCode();
        pagina = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/fundaciones/{ses.Usuario.Id}");
        Assert.Equal(JsonValueKind.Null, pagina.GetProperty("nit").ValueKind);

        // Una cuenta que no es fundación verificada no tiene página
        var otra = await Api.RegistrarSesionAsync(_f, "noFunda");
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/v1/fundaciones/{otra.Usuario.Id}")).StatusCode);
    }

    [Fact]
    public async Task Necesidades_maximo_diez_solo_su_duenia_las_edita_y_se_borran_al_cubrirse()
    {
        var (_, c, _) = await FundacionAprobadaAsync("fundaNecesita");
        var ids = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            var r = await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = $"Necesidad número {i}" });
            Assert.Equal(HttpStatusCode.Created, r.StatusCode);
            ids.Add((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "La número once" })).StatusCode);

        var (_, otra, _) = await FundacionAprobadaAsync("fundaIntrusa");
        Assert.Equal(HttpStatusCode.NotFound, (await otra.PutAsJsonAsync($"/api/v1/fundaciones/mia/necesidades/{ids[0]}", new { texto = "Robada" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otra.DeleteAsync($"/api/v1/fundaciones/mia/necesidades/{ids[0]}")).StatusCode);

        var editada = await c.PutAsJsonAsync($"/api/v1/fundaciones/mia/necesidades/{ids[0]}", new { texto = "Pañales etapa 3", cantidad = 20, urgente = true });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        Assert.True((await editada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("urgente").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/v1/fundaciones/mia/necesidades/{ids[0]}")).StatusCode);
        Assert.Equal(9, (await c.GetFromJsonAsync<JsonElement>("/api/v1/fundaciones/mia")).GetProperty("necesidades").GetArrayLength());
    }

    [Fact]
    public async Task Dejar_de_ser_fundacion_o_eliminar_la_cuenta_borra_el_perfil_y_la_insignia()
    {
        var (ses, c, _) = await FundacionAprobadaAsync("fundaSeVa");
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync("/api/v1/fundaciones/mia")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.GetAsync("/api/v1/fundaciones/mia")).StatusCode);
        var perfil = await _f.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.Equal(JsonValueKind.Null, perfil.GetProperty("nombreFundacion").ValueKind);

        var (ses2, c2, _) = await FundacionAprobadaAsync("fundaBorrada");
        var export = await c2.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/datos");
        Assert.Equal("Aprobada", export.GetProperty("fundacion").GetProperty("estado").GetString());     // derecho a conocer
        var super = await SuperAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await super.PostAsJsonAsync($"/api/v1/admin/usuarios/{ses2.Usuario.Id}/eliminar", new { motivo = "Cuenta de prueba" })).StatusCode);
        var lista = await _f.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/fundaciones");
        Assert.DoesNotContain(lista.EnumerateArray(), x => x.GetProperty("id").GetGuid() == ses2.Usuario.Id);
    }
}
