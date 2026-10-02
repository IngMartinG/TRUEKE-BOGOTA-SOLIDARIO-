using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class RefrescoTests : IClassFixture<FabricaApi>
{
    private const string Cookie = "__Secure-trueke_rt";
    private readonly FabricaApi _fabrica;
    public RefrescoTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static string SetCookie(HttpResponseMessage r)
        => r.Headers.TryGetValues("Set-Cookie", out var v) ? v.Single(c => c.StartsWith(Cookie + "=", StringComparison.Ordinal)) : "";

    private static string ValorCookie(HttpResponseMessage r) => SetCookie(r).Split(';')[0][(Cookie.Length + 1)..];

    private static Task<HttpResponseMessage> RefrescarAsync(WebApplicationFactory<Program> f, string? cookie, bool csrf = true, string? origen = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refrescar");
        if (cookie is not null) req.Headers.Add("Cookie", $"{Cookie}={cookie}");
        if (csrf) req.Headers.Add("X-Trueke-Csrf", "1");
        if (origen is not null) req.Headers.Add("Origin", origen);
        return f.CreateClient().SendAsync(req);
    }

    private async Task<(HttpResponseMessage Resp, string Cookie)> RegistrarAsync()
    {
        var r = await _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        {
            nombreCompleto = "Refresco Prueba", localidad = "Usaquén", correo = $"rf{Guid.NewGuid():N}@trueke.test",
            clave = Api.ClaveValida, aceptoPoliticaDatos = true
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (r, ValorCookie(r));
    }

    [Fact]
    public async Task El_refresco_va_en_cookie_HttpOnly_Secure_SameSite_Strict_y_nunca_en_el_JSON()
    {
        var (resp, cookie) = await RegistrarAsync();
        var sc = SetCookie(resp).ToLowerInvariant();
        Assert.Contains("httponly", sc);
        Assert.Contains("secure", sc);
        Assert.Contains("samesite=strict", sc);
        Assert.Contains("path=/api/v1/auth", sc);
        Assert.DoesNotContain(cookie, await resp.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refrescar_rota_el_token_y_entrega_un_acceso_valido()
    {
        var (_, cookie) = await RegistrarAsync();
        var r = await RefrescarAsync(_fabrica, cookie);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var nuevaCookie = ValorCookie(r);
        Assert.NotEqual(cookie, nuevaCookie);

        var sesion = (await r.Content.ReadFromJsonAsync<SesionMinDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await Api.ConToken(_fabrica, sesion.Token).GetAsync("/api/v1/usuarios/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefrescarAsync(_fabrica, nuevaCookie)).StatusCode);
    }

    [Fact]
    public async Task Reusar_un_refresco_viejo_revoca_toda_la_sesion()
    {
        var (_, original) = await RegistrarAsync();
        var r1 = await RefrescarAsync(_fabrica, original);
        var legitima = ValorCookie(r1);

        // un atacante reusa el token robado (ya rotado)
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefrescarAsync(_fabrica, original)).StatusCode);
        // y la sesión legítima también queda cortada
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefrescarAsync(_fabrica, legitima)).StatusCode);
    }

    [Fact]
    public async Task Dentro_de_la_ventana_de_gracia_el_reuso_responde_409_sin_revocar()
    {
        await using var f = _fabrica.WithWebHostBuilder(b => b.UseSetting("Seguridad:SegundosGraciaRefresco", "60"));
        var registro = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        {
            nombreCompleto = "Pestanas Prueba", localidad = "Bosa", correo = $"tab{Guid.NewGuid():N}@trueke.test",
            clave = Api.ClaveValida, aceptoPoliticaDatos = true
        });
        var original = ValorCookie(registro);
        var nueva = ValorCookie(await RefrescarAsync(f, original));

        Assert.Equal(HttpStatusCode.Conflict, (await RefrescarAsync(f, original)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefrescarAsync(f, nueva)).StatusCode);
    }

    [Fact]
    public async Task Sin_cabecera_CSRF_o_con_origen_ajeno_es_403()
    {
        var (_, cookie) = await RegistrarAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await RefrescarAsync(_fabrica, cookie, csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RefrescarAsync(_fabrica, cookie, origen: "https://sitio-malicioso.example")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await RefrescarAsync(_fabrica, cookie, origen: "http://localhost:4200")).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("corto")]
    [InlineData("token-inventado-que-no-existe-en-la-base-0000000")]
    public async Task Sin_cookie_o_con_token_invalido_es_401(string? cookie)
        => Assert.Equal(HttpStatusCode.Unauthorized, (await RefrescarAsync(_fabrica, cookie)).StatusCode);

    [Fact]
    public async Task Salir_revoca_el_refresco_y_borra_la_cookie()
    {
        var (_, cookie) = await RegistrarAsync();
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/salir");
        req.Headers.Add("Cookie", $"{Cookie}={cookie}");
        req.Headers.Add("X-Trueke-Csrf", "1");
        var r = await _fabrica.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Contains("expires=thu, 01 jan 1970", SetCookie(r).ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefrescarAsync(_fabrica, cookie)).StatusCode);
    }

    [Fact]
    public async Task Cambiar_la_clave_o_cerrar_sesiones_revoca_los_refrescos()
    {
        var (resp, cookie) = await RegistrarAsync();
        var sesion = (await resp.Content.ReadFromJsonAsync<SesionMinDto>())!;
        var c = Api.ConToken(_fabrica, sesion.Token);
        (await c.PostAsync("/api/v1/auth/cerrar-sesiones", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await RefrescarAsync(_fabrica, cookie)).StatusCode);
    }
}
