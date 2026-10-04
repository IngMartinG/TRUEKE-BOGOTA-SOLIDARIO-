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
        => c.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "Lámpara", descripcion = "Funciona", categoriaId = 4, modo = "Trueke", condicion = "ComoNuevo", localidad = "Kennedy", imagenes = imagenUrl is null ? null : new[] { imagenUrl } });

    [Fact]
    public async Task Flujo_completo_pedir_subida_subir_y_publicar_con_la_imagen()
    {
        var c = await Api.RegistrarAsync(_f, "fotos");
        var s = await SubidaAsync(c);
        _almacen.Subir(s.UrlArchivo, "image/png", AlmacenFalso.Png());

        var r = await PublicarAsync(c, s.UrlArchivo);
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        Assert.Equal(s.UrlArchivo, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imagenes")[0].GetString());
    }

    [Fact]
    public async Task Foto_de_perfil_propia_se_ve_en_el_perfil_publico_y_al_cambiarla_se_borra_la_anterior()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "conFoto");
        var c = Api.ConToken(_f, ses.Token);
        var s1 = await SubidaAsync(c);
        _almacen.Subir(s1.UrlArchivo, "image/png", AlmacenFalso.Png());
        var r = await c.PutAsJsonAsync("/api/v1/usuarios/yo/foto", new { url = s1.UrlArchivo });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(s1.UrlArchivo, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("fotoUrl").GetString());

        // Un tercero (incluso anónimo) la ve en el perfil público
        var perfil = await _f.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.Equal(s1.UrlArchivo, perfil.GetProperty("fotoUrl").GetString());

        // Cambiarla borra la anterior del almacenamiento; quitarla vuelve a las iniciales
        var s2 = await SubidaAsync(c);
        _almacen.Subir(s2.UrlArchivo, "image/png", AlmacenFalso.Png());
        (await c.PutAsJsonAsync("/api/v1/usuarios/yo/foto", new { url = s2.UrlArchivo })).EnsureSuccessStatusCode();
        Assert.False(_almacen.Existe(s1.UrlArchivo));
        var sinFoto = await (await c.DeleteAsync("/api/v1/usuarios/yo/foto")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, sinFoto.GetProperty("fotoUrl").ValueKind);
        Assert.False(_almacen.Existe(s2.UrlArchivo));

        // No se puede usar la imagen de otra persona ni una URL externa
        var ajeno = await Api.RegistrarAsync(_f, "ajenoFoto");
        var sa = await SubidaAsync(ajeno);
        _almacen.Subir(sa.UrlArchivo, "image/png", AlmacenFalso.Png());
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/v1/usuarios/yo/foto", new { url = sa.UrlArchivo })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/v1/usuarios/yo/foto", new { url = "https://rastreador.example/x.png" })).StatusCode);
        Assert.True(_almacen.Existe(sa.UrlArchivo));
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
    public async Task Documento_de_identidad_privado_se_ve_con_enlace_temporal_y_se_borra_al_resolver()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "verificame");
        var c = Api.ConToken(_f, ses.Token);
        var s = await SubidaAsync(c, "application/pdf", 2000, "Documento");
        Assert.Contains("/documentos/", s.UrlArchivo);
        _almacen.Subir(s.UrlArchivo, "application/pdf", "%PDF-1.7 cedula"u8.ToArray());

        var pago = await c.PostAsJsonAsync("/api/v1/pagos/iniciar", new { concepto = "Verificar", documentoUrl = s.UrlArchivo });
        Assert.True(pago.StatusCode == HttpStatusCode.Created, await pago.Content.ReadAsStringAsync());
        var referencia = (await pago.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("referencia").GetString();
        (await c.PostAsync($"/api/v1/pagos/{referencia}/simular?aprobado=true", null)).EnsureSuccessStatusCode();

        var super = Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var pendientes = await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/verificaciones");
        var mia = pendientes.EnumerateArray().Single(v => v.GetProperty("usuarioId").GetGuid() == ses.Usuario.Id);
        Assert.EndsWith("?sas=lectura", mia.GetProperty("documentoUrl").GetString());   // enlace temporal, no la URL directa

        Assert.Equal(HttpStatusCode.NoContent, (await super.PostAsync($"/api/v1/admin/verificaciones/{ses.Usuario.Id}/aprobar", null)).StatusCode);
        Assert.False(_almacen.Existe(s.UrlArchivo));                                     // minimización: el documento ya no se guarda
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
