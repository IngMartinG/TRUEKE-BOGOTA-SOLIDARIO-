using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Lo que el front Angular puede dar por garantizado.</summary>
public partial class ContratoAngularTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ContratoAngularTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task<JsonElement> ProblemaAsync(HttpResponseMessage r, HttpStatusCode esperado)
    {
        Assert.Equal(esperado, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        var p = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)esperado, p.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("title").GetString()));
        Assert.StartsWith("https://", p.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("traceId").GetString()));
        return p;
    }

    [Fact]
    public async Task Todos_los_errores_son_ProblemDetails()
    {
        var anonimo = _fabrica.CreateClient();
        var cliente = await Api.RegistrarAsync(_fabrica, "contrato");

        await ProblemaAsync(await anonimo.GetAsync("/api/v1/usuarios/yo"), HttpStatusCode.Unauthorized);
        await ProblemaAsync(await cliente.GetAsync("/api/v1/admin/verificaciones"), HttpStatusCode.Forbidden);
        await ProblemaAsync(await anonimo.GetAsync($"/api/v1/publicaciones/{Guid.NewGuid()}"), HttpStatusCode.NotFound);   // de dominio
        // Rutas inexistentes: el invitado recibe 401 (FallbackPolicy: no se revela qué rutas existen); con sesión, 404/405
        await ProblemaAsync(await anonimo.GetAsync("/api/v1/no-existe"), HttpStatusCode.Unauthorized);
        await ProblemaAsync(await cliente.GetAsync("/api/v1/no-existe"), HttpStatusCode.NotFound);
        await ProblemaAsync(await cliente.DeleteAsync("/api/v1/categorias"), HttpStatusCode.MethodNotAllowed);
        await ProblemaAsync(await cliente.GetAsync("/api/v1/eco-puntos/cotizacion?concepto=Recarga"), HttpStatusCode.NotFound);

        var regla = await ProblemaAsync(await cliente.PostAsJsonAsync("/api/v1/solicitudes",
            new { publicacionId = await Api.CrearPublicacionAsync(cliente), mensaje = "propia" }), HttpStatusCode.BadRequest);
        Assert.Equal("No puedes solicitar tu propia publicación.", regla.GetProperty("title").GetString());

        await ProblemaAsync(await cliente.PostAsJsonAsync("/api/v1/publicaciones", new { descripcion = new string('a', 1_100_000) }),
            HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Validacion_devuelve_errores_por_campo_en_camelCase_y_en_espanol()
    {
        var cliente = await Api.RegistrarAsync(_fabrica, "valida");
        var r = await cliente.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "ab", descripcion = "", categoriaId = 0, modo = "Trueke", localidad = "B" });
        var p = await ProblemaAsync(r, HttpStatusCode.BadRequest);
        var errores = p.GetProperty("errors");
        Assert.True(errores.TryGetProperty("titulo", out var titulo));
        Assert.True(errores.TryGetProperty("localidad", out _));
        Assert.Contains("caracteres", titulo[0].GetString());
        Assert.DoesNotContain("The field", p.GetRawText());
    }

    [Fact]
    public async Task Json_malformado_no_revela_tipos_internos()
    {
        var cliente = await Api.RegistrarAsync(_fabrica, "malformado");
        var r = await cliente.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "Titulo ok", descripcion = "x", categoriaId = 1, modo = "Robo", localidad = "Usme" });
        var p = await ProblemaAsync(r, HttpStatusCode.BadRequest);
        Assert.DoesNotContain("TruekeBogotaSolidario", p.GetRawText());
        Assert.DoesNotContain("LineNumber", p.GetRawText());
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$")]
    private static partial Regex IsoUtc();

    [Fact]
    public async Task Las_fechas_son_UTC_ISO8601_con_Z()
    {
        var c = await Api.RegistrarAsync(_fabrica, "fechas");
        var id = await Api.CrearPublicacionAsync(c);
        var p = await (await c.GetAsync($"/api/v1/publicaciones/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        var fecha = p.GetProperty("fechaPublicacion").GetString()!;
        Assert.Matches(IsoUtc(), fecha);
        Assert.InRange(DateTime.Parse(fecha, null, System.Globalization.DateTimeStyles.RoundtripKind), DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task CORS_permite_el_origen_configurado_y_rechaza_otros()
    {
        var anonimo = _fabrica.CreateClient();

        var permitido = new HttpRequestMessage(HttpMethod.Options, "/api/v1/publicaciones");
        permitido.Headers.Add("Origin", "http://localhost:4200");
        permitido.Headers.Add("Access-Control-Request-Method", "POST");
        permitido.Headers.Add("Access-Control-Request-Headers", "authorization,content-type");
        var r = await anonimo.SendAsync(permitido);
        Assert.Equal("http://localhost:4200", r.Headers.GetValues("Access-Control-Allow-Origin").Single());

        var simple = new HttpRequestMessage(HttpMethod.Get, "/api/v1/categorias");
        simple.Headers.Add("Origin", "http://localhost:4200");
        var rs = await anonimo.SendAsync(simple);
        Assert.Contains("Location", rs.Headers.GetValues("Access-Control-Expose-Headers").Single());

        var malicioso = new HttpRequestMessage(HttpMethod.Options, "/api/v1/publicaciones");
        malicioso.Headers.Add("Origin", "https://sitio-malicioso.example");
        malicioso.Headers.Add("Access-Control-Request-Method", "POST");
        var rm = await anonimo.SendAsync(malicioso);
        Assert.False(rm.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Rate_limit_responde_429_ProblemDetails_con_Retry_After()
    {
        await using var f = _fabrica.WithWebHostBuilder(b => b.UseSetting("RateLimiting:AuthPorMinuto", "2"));
        var c = f.CreateClient();
        HttpResponseMessage? ultima = null;
        for (var i = 0; i < 3; i++)
            ultima = await c.PostAsJsonAsync("/api/v1/auth/login", new { correo = "x@trueke.test", clave = "Algo12345" });
        await ProblemaAsync(ultima!, HttpStatusCode.TooManyRequests);
        Assert.True(ultima!.Headers.Contains("Retry-After"));
    }
}
