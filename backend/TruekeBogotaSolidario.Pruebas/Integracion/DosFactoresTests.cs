using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class DosFactoresTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public DosFactoresTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static byte[] DesdeBase32(string s)
    {
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in s)
        {
            buffer = (buffer << 5) | alfabeto.IndexOf(c);
            bits += 5;
            if (bits >= 8) { bytes.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        return bytes.ToArray();
    }

    /// <summary>Lo que mostraría la app autenticadora dentro de <paramref name="pasos"/> pasos de 30 s.</summary>
    private static string Codigo(byte[] secreto, int pasos = 0) => Totp.Calcular(secreto, Totp.Paso(DateTimeOffset.UtcNow) + pasos);

    private static async Task<(byte[] Secreto, IReadOnlyList<string> Codigos, string Token)> ActivarAsync(HttpClient c)
    {
        var cfg = await (await c.PostAsync("/api/v1/auth/2fa/configurar", null)).Content.ReadFromJsonAsync<ConfiguracionDosFactoresDto>();
        Assert.StartsWith("otpauth://totp/", cfg!.UriOtpauth);
        var secreto = DesdeBase32(cfg.SecretoBase32);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/2fa/activar", new { codigo = "000000" })).StatusCode);
        var r = await c.PostAsJsonAsync("/api/v1/auth/2fa/activar", new { codigo = Codigo(secreto) });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var act = (await r.Content.ReadFromJsonAsync<ActivacionDosFactoresDto>())!;
        return (secreto, act.CodigosRecuperacion, act.Sesion.Token);
    }

    private Task<HttpResponseMessage> LoginAsync(string correo, string? codigo)
        => _fabrica.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = Api.ClaveValida, codigoDosFactores = codigo });

    [Fact]
    public async Task Activar_exige_el_codigo_en_cada_login_y_los_de_recuperacion_sirven_una_vez()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "seguro");
        var c = Api.ConToken(_fabrica, ses.Token);
        var (secreto, codigos, tokenNuevo) = await ActivarAsync(c);
        Assert.Equal(10, codigos.Count);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/usuarios/yo")).StatusCode);   // sesión previa revocada
        var yo = await Api.ConToken(_fabrica, tokenNuevo).GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo");
        Assert.True(yo.GetProperty("dosFactoresActivo").GetBoolean());

        var sinCodigo = await LoginAsync(ses.Usuario.Correo, null);
        Assert.Equal(HttpStatusCode.Unauthorized, sinCodigo.StatusCode);
        Assert.Equal("2fa_requerido", (await sinCodigo.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(ses.Usuario.Correo, "123456")).StatusCode);

        var ok = await LoginAsync(ses.Usuario.Correo, Codigo(secreto, 1));              // paso siguiente (el actual ya se usó al activar)
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(ses.Usuario.Correo, Codigo(secreto, 1))).StatusCode); // no se repite

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(ses.Usuario.Correo, codigos[0])).StatusCode);       // recuperación
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(ses.Usuario.Correo, codigos[0])).StatusCode); // una sola vez
    }

    [Fact]
    public async Task Desactivar_exige_un_codigo_valido()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "desactiva");
        var (_, codigos, token) = await ActivarAsync(Api.ConToken(_fabrica, ses.Token));
        var c = Api.ConToken(_fabrica, token);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/2fa/desactivar", new { codigo = "000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/v1/auth/2fa/desactivar", new { codigo = codigos[1] })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(ses.Usuario.Correo, null)).StatusCode);
    }

    [Fact]
    public async Task Con_la_exigencia_activa_un_admin_necesita_sesion_con_2FA()
    {
        await using var f = new FabricaApiDosFactores();
        var super = Api.ConToken(f, await Api.LoginAsync(f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var r = await super.GetAsync("/api/v1/admin/verificaciones");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Equal(DosFactoresCodigo, (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("codigo").GetString());

        var (secreto, _, tokenMfa) = await ActivarAsync(super);
        Assert.Equal(HttpStatusCode.OK, (await Api.ConToken(f, tokenMfa).GetAsync("/api/v1/admin/verificaciones")).StatusCode);

        // la marca 2FA sobrevive al refresco del token
        var login = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/login",
            new { correo = FabricaApi.CorreoSuper, clave = FabricaApi.ClaveSuper, codigoDosFactores = Codigo(secreto, 1) });
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refrescar");
        req.Headers.Add("Cookie", cookie);
        req.Headers.Add("X-Trueke-Csrf", "1");
        var refrescada = (await (await f.CreateClient().SendAsync(req)).Content.ReadFromJsonAsync<SesionMinDto>())!;
        Assert.Equal(HttpStatusCode.OK, (await Api.ConToken(f, refrescada.Token).GetAsync("/api/v1/admin/verificaciones")).StatusCode);
    }

    private const string DosFactoresCodigo = "2fa_requerido_admin";

    private sealed class FabricaApiDosFactores : FabricaApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Seguridad:ExigirDosFactoresModeradores", "true");
        }
    }
}
