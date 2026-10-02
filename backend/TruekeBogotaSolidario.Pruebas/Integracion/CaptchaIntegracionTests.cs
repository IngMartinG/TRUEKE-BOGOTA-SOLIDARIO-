using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class CaptchaIntegracionTests : IClassFixture<FabricaApi>
{
    /// <summary>Solo acepta "humano-{accion}".</summary>
    private sealed class CaptchaFalso : IVerificadorCaptcha
    {
        public bool Habilitado => true;
        public Task ExigirAsync(string? token, string accion, CancellationToken ct = default)
            => token == $"humano-{accion}" ? Task.CompletedTask : throw new ReglaDeNegocioException("No pudimos verificar que eres una persona.");
    }

    private readonly WebApplicationFactory<Program> _f;

    public CaptchaIntegracionTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b =>
        {
            b.UseSetting("Captcha:ClaveSitio", "clave-publica-de-sitio");
            b.UseSetting("Captcha:ClaveSecreta", "secreto");
            b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Transient<IVerificadorCaptcha, CaptchaFalso>()));
        });

    private static object Registro(string correo, string? captcha)
        => new { nombreCompleto = "Captcha Prueba", localidad = "Suba", correo, clave = Api.ClaveValida, aceptoPoliticaDatos = true, captchaToken = captcha };

    [Fact]
    public async Task Registro_login_y_olvide_clave_exigen_captcha_valido_de_su_accion()
    {
        var c = _f.CreateClient();
        var correo = $"cap{Guid.NewGuid():N}@trueke.test";
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/registrar", Registro(correo, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/registrar", Registro(correo, "humano-login"))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/v1/auth/registrar", Registro(correo, "humano-registro"))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = Api.ClaveValida })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/v1/auth/login", new { correo, clave = Api.ClaveValida, captchaToken = "humano-login" })).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/auth/olvide-clave", new { correo })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await c.PostAsJsonAsync("/api/v1/auth/olvide-clave", new { correo, captchaToken = "humano-olvide_clave" })).StatusCode);
    }

    [Fact]
    public async Task Configuracion_publica_expone_la_clave_de_sitio_y_nunca_la_secreta()
    {
        var r = await _f.CreateClient().GetAsync("/api/v1/configuracion");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secreto", texto);
        var cfg = JsonDocument.Parse(texto).RootElement;
        Assert.Equal("clave-publica-de-sitio", cfg.GetProperty("captchaClaveSitio").GetString());
        Assert.Equal(5, cfg.GetProperty("maxImagenesPorPublicacion").GetInt32());
        Assert.Equal("2026-10", cfg.GetProperty("versionPoliticaDatos").GetString());
    }
}
