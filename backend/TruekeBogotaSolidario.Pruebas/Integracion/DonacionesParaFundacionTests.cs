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
/// "Tengo esto": al publicar una donación para una necesidad, la fundación verificada recibe un aviso con enlace a la publicación
/// (sin el correo de quien dona), como mucho uno por donante cada 24 h, y nunca si no está aprobada, hay bloqueo o no es Donación.
/// </summary>
public class DonacionesParaFundacionTests : IClassFixture<FabricaApi>
{
    private const string Tipo = "DonacionParaFundacion";
    private readonly AlmacenFalso _almacen = new();
    private readonly RelojFalso _reloj = new();
    private readonly WebApplicationFactory<Program> _f;

    public DonacionesParaFundacionTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen));
            s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_reloj));
        }));

    private static string NitNuevo() => DocumentosFiscales.NormalizarNit("9" + Random.Shared.Next(10_000_000, 99_999_999));

    /// <summary>Fundación con una necesidad; si <paramref name="aprobar"/> es false queda en revisión.</summary>
    private async Task<(SesionMinDto Ses, HttpClient C, Guid NecesidadId)> FundacionConNecesidadAsync(string alias, bool aprobar = true)
    {
        var ses = await Api.RegistrarSesionAsync(_f, alias);
        var c = Api.ConToken(_f, ses.Token);
        var subida = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "Documento", contentType = "application/pdf", tamanoBytes = 2000 });
        var s = (await subida.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
        _almacen.Subir(s.UrlArchivo, "application/pdf", "%PDF-1.7 certificado camara de comercio"u8.ToArray());
        (await c.PostAsJsonAsync("/api/v1/fundaciones/mia", new
        {
            razonSocial = $"Fundación {alias}", nit = NitNuevo(), mostrarNit = false, declaroRepresentacion = true, certificadoUrl = s.UrlArchivo,
            descripcion = "Acompañamos a 60 niños de Bosa con refuerzo escolar y almuerzo los sábados."
        })).EnsureSuccessStatusCode();
        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        Assert.Equal(HttpStatusCode.NoContent, (await super.PostAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/aprobar", null)).StatusCode);
        var r = await c.PostAsJsonAsync("/api/v1/fundaciones/mia/necesidades", new { texto = "Cuadernos cuadriculados", categoriaId = 1 });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var necesidad = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (!aprobar)
            Assert.Equal(HttpStatusCode.NoContent,
                (await super.PostAsJsonAsync($"/api/v1/admin/fundaciones/{ses.Usuario.Id}/retirar", new { motivo = "Se disolvió" })).StatusCode);
        return (ses, c, necesidad);
    }

    private static async Task<Guid> DonarAsync(HttpClient c, Guid necesidad, string modo = "Donacion")
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Cuadernos nuevos", descripcion = "Veinte cuadernos sin usar", categoriaId = 1, modo, condicion = "Nuevo",
            localidad = "Bosa", precioReferenciaCop = modo == "Compra" ? 20000 : (decimal?)null, paraNecesidadId = necesidad
        });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    private static async Task<JsonElement[]> AvisosAsync(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones?tamano=50")).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("tipo").GetString() == Tipo).ToArray();

    [Fact]
    public async Task Avisa_a_la_fundacion_con_enlace_a_la_publicacion_sin_el_correo_de_quien_dona()
    {
        var (fundacion, c, necesidad) = await FundacionConNecesidadAsync("fundaAvisada");
        var donante = await Api.RegistrarSesionAsync(_f, "donaCuadernos");
        var pubId = await DonarAsync(Api.ConToken(_f, donante.Token), necesidad);

        var aviso = Assert.Single(await AvisosAsync(c));
        Assert.Equal(pubId, aviso.GetProperty("recursoId").GetGuid());
        var mensaje = aviso.GetProperty("mensaje").GetString()!;
        Assert.Contains("Cuadernos nuevos", mensaje);
        Assert.Contains("Cuadernos cuadriculados", mensaje);
        Assert.DoesNotContain(donante.Usuario.Correo, mensaje, StringComparison.OrdinalIgnoreCase);

        var correo = Api.Correos(_f).Para(fundacion.Usuario.Correo).Last();                       // no tiene la app abierta
        Assert.Contains($"/publicacion/{pubId}", correo.Texto);
        Assert.DoesNotContain(donante.Usuario.Correo, correo.Texto, StringComparison.OrdinalIgnoreCase);

        // La publicación es igual a cualquier otra: visible y disponible para todos
        var pub = await _f.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pubId}");
        Assert.Equal("Disponible", pub.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Como_mucho_un_aviso_por_donante_y_fundacion_cada_24_horas()
    {
        var (_, c, necesidad) = await FundacionConNecesidadAsync("fundaLimite");
        var donante = Api.ConToken(_f, (await Api.RegistrarSesionAsync(_f, "donaMucho")).Token);
        await DonarAsync(donante, necesidad);
        await DonarAsync(donante, necesidad);
        Assert.Single(await AvisosAsync(c));

        var otro = Api.ConToken(_f, (await Api.RegistrarSesionAsync(_f, "donaOtro")).Token);    // otra persona sí avisa
        await DonarAsync(otro, necesidad);
        Assert.Equal(2, (await AvisosAsync(c)).Length);

        _reloj.Avanzar(TimeSpan.FromHours(25));
        await DonarAsync(donante, necesidad);
        Assert.Equal(3, (await AvisosAsync(c)).Length);
    }

    [Fact]
    public async Task Sin_aviso_si_la_fundacion_ya_no_esta_verificada()
    {
        var (_, c, necesidad) = await FundacionConNecesidadAsync("fundaRetirada", aprobar: false);
        var donante = Api.ConToken(_f, (await Api.RegistrarSesionAsync(_f, "donaRetirada")).Token);
        await DonarAsync(donante, necesidad);                                                     // la publicación se crea igual
        Assert.Empty(await AvisosAsync(c));
    }

    [Fact]
    public async Task Sin_aviso_si_hay_bloqueo_en_cualquier_direccion()
    {
        var (fundacion, c, necesidad) = await FundacionConNecesidadAsync("fundaBloquea");
        var sesDonante = await Api.RegistrarSesionAsync(_f, "donaBloqueado");
        var donante = Api.ConToken(_f, sesDonante.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/v1/usuarios/{sesDonante.Usuario.Id}/bloqueo", null)).StatusCode);
        await DonarAsync(donante, necesidad);
        Assert.Empty(await AvisosAsync(c));

        var sesOtro = await Api.RegistrarSesionAsync(_f, "donaQueBloquea");
        var otro = Api.ConToken(_f, sesOtro.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await otro.PostAsync($"/api/v1/usuarios/{fundacion.Usuario.Id}/bloqueo", null)).StatusCode);
        await DonarAsync(otro, necesidad);
        Assert.Empty(await AvisosAsync(c));
    }

    [Fact]
    public async Task Sin_aviso_si_no_es_donacion_o_la_necesidad_no_existe()
    {
        var (_, c, necesidad) = await FundacionConNecesidadAsync("fundaTrueke");
        var donante = Api.ConToken(_f, (await Api.RegistrarSesionAsync(_f, "noDona")).Token);
        await DonarAsync(donante, necesidad, modo: "Trueke");
        await DonarAsync(donante, Guid.NewGuid());
        Assert.Empty(await AvisosAsync(c));
    }
}
