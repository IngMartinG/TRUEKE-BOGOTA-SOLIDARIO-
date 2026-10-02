using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class ImagenesTests : IClassFixture<FabricaApi>
{
    private readonly AlmacenFalso _almacen = new();
    private readonly WebApplicationFactory<Program> _f;

    public ImagenesTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen))));

    private static async Task<SubidaArchivoDto> SubidaAsync(HttpClient c, string contentType = "image/png", long tamano = 100, string tipo = "Imagen")
    {
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo, contentType, tamanoBytes = tamano });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient c, string? imagenUrl)
        => c.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "Lámpara", descripcion = "Funciona", categoriaId = 4, modo = "Trueke", localidad = "Kennedy", imagenUrl });

    [Fact]
    public async Task Flujo_completo_pedir_subida_subir_y_publicar_con_la_imagen()
    {
        var c = await Api.RegistrarAsync(_f, "fotos");
        var s = await SubidaAsync(c);
        _almacen.Subir(s.UrlArchivo, "image/png", AlmacenFalso.Png());

        var r = await PublicarAsync(c, s.UrlArchivo);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal(s.UrlArchivo, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imagenUrl").GetString());
    }

    [Fact]
    public async Task No_se_puede_usar_la_imagen_de_otro_usuario_ni_una_url_externa()
    {
        var ajeno = await Api.RegistrarAsync(_f, "ajeno");
        var s = await SubidaAsync(ajeno);
        _almacen.Subir(s.UrlArchivo, "image/png", AlmacenFalso.Png());

        var yo = await Api.RegistrarAsync(_f, "yo");
        Assert.Equal(HttpStatusCode.BadRequest, (await PublicarAsync(yo, s.UrlArchivo)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PublicarAsync(yo, "https://rastreador.example/pixel.png")).StatusCode);
    }

    [Fact]
    public async Task Archivo_no_subido_o_con_contenido_falso_es_rechazado()
    {
        var c = await Api.RegistrarAsync(_f, "falso");
        var noSubido = await SubidaAsync(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await PublicarAsync(c, noSubido.UrlArchivo)).StatusCode);

        var disfrazado = await SubidaAsync(c);
        _almacen.Subir(disfrazado.UrlArchivo, "image/png", "<html><script>alert(1)</script></html>"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await PublicarAsync(c, disfrazado.UrlArchivo)).StatusCode);

        var grande = await SubidaAsync(c);
        _almacen.Subir(grande.UrlArchivo, "image/png", AlmacenFalso.Png(6 * 1024 * 1024)); // la SAS no limita tamaño: se valida después
        Assert.Equal(HttpStatusCode.BadRequest, (await PublicarAsync(c, grande.UrlArchivo)).StatusCode);
    }

    [Theory]
    [InlineData("image/svg+xml", 100)]
    [InlineData("text/html", 100)]
    [InlineData("image/png", 6 * 1024 * 1024)]
    public async Task Pedir_subida_valida_tipo_y_tamano(string contentType, long tamano)
    {
        var c = await Api.RegistrarAsync(_f, "valida");
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "Imagen", contentType, tamanoBytes = tamano });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Pedir_subida_exige_sesion_y_correo_verificado()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PostAsJsonAsync("/api/v1/archivos/subidas",
            new { tipo = "Imagen", contentType = "image/png", tamanoBytes = 100 })).StatusCode);

        var r = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        { nombreCompleto = "Sin Verificar", localidad = "Bosa", correo = $"sv{Guid.NewGuid():N}@trueke.test", clave = Api.ClaveValida, aceptoPoliticaDatos = true });
        var token = (await r.Content.ReadFromJsonAsync<SesionMinDto>())!.Token;
        Assert.Equal(HttpStatusCode.Forbidden, (await Api.ConToken(_f, token).PostAsJsonAsync("/api/v1/archivos/subidas",
            new { tipo = "Imagen", contentType = "image/png", tamanoBytes = 100 })).StatusCode);
    }
}
