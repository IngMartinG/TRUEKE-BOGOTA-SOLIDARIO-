using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El verificador real contra una respuesta simulada de Turnstile siteverify.</summary>
public class CaptchaTests
{
    private sealed class TurnstileSimulado : HttpMessageHandler
    {
        private readonly Func<string, (HttpStatusCode, string)> _responder;
        public string? CuerpoRecibido { get; private set; }
        public Uri? UrlRecibida { get; private set; }
        public TurnstileSimulado(Func<string, (HttpStatusCode, string)> responder) => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            UrlRecibida = request.RequestUri;
            CuerpoRecibido = await request.Content!.ReadAsStringAsync(ct);
            var (estado, json) = _responder(CuerpoRecibido);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static VerificadorTurnstile Crear(TurnstileSimulado turnstile, string secreto = "secreto-servidor")
        => new(new HttpClient(turnstile), Options.Create(new CaptchaOpciones { ClaveSecreta = secreto, ClaveSitio = "sitio" }),
            NullLogger<VerificadorTurnstile>.Instance);

    private static TurnstileSimulado Responde(string json) => new(_ => (HttpStatusCode.OK, json));

    [Fact]
    public async Task Persona_con_accion_correcta_pasa_y_se_envia_el_secreto_a_Cloudflare()
    {
        var turnstile = Responde("{\"success\":true,\"action\":\"registro\",\"hostname\":\"localhost\"}");
        await Crear(turnstile).ExigirAsync("token-del-navegador", AccionesCaptcha.Registro);
        Assert.Equal("challenges.cloudflare.com", turnstile.UrlRecibida?.Host);
        Assert.Contains("secret=secreto-servidor", turnstile.CuerpoRecibido);
        Assert.Contains("response=token-del-navegador", turnstile.CuerpoRecibido);
    }

    [Theory]
    [InlineData("{\"success\":true,\"action\":\"login\"}")]                          // token de otra acción (reutilizado)
    [InlineData("{\"success\":true}")]                                               // sin acción
    [InlineData("{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}")]  // vencido o ya usado
    [InlineData("no es json")]
    public async Task Rechaza_tokens_de_otra_accion_vencidos_o_respuestas_raras(string json)
        => await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Crear(Responde(json)).ExigirAsync("t", AccionesCaptcha.Registro));

    [Fact]
    public async Task Sin_token_con_token_gigante_o_si_Cloudflare_no_responde_falla_cerrado()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Crear(Responde("{}")).ExigirAsync(null, AccionesCaptcha.Login));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Crear(Responde("{\"success\":true,\"action\":\"login\"}")).ExigirAsync(new string('a', 2049), AccionesCaptcha.Login));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            Crear(new TurnstileSimulado(_ => (HttpStatusCode.ServiceUnavailable, ""))).ExigirAsync("t", AccionesCaptcha.Login));
    }

    [Fact]
    public async Task Solo_con_la_clave_de_prueba_se_tolera_la_respuesta_sin_accion()
    {
        const string sinAccion = "{\"success\":true}";
        await Crear(Responde(sinAccion), secreto: "1x0000000000000000000000000000000AA").ExigirAsync("XXXX.DUMMY.TOKEN.XXXX", AccionesCaptcha.Login);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Crear(Responde(sinAccion)).ExigirAsync("t", AccionesCaptcha.Login));
        // Aun con la clave de prueba, una acción distinta se rechaza.
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            Crear(Responde("{\"success\":true,\"action\":\"registro\"}"), secreto: "1x0000000000000000000000000000000AA").ExigirAsync("t", AccionesCaptcha.Login));
    }

    [Theory]
    [InlineData("1x0000000000000000000000000000000AA", true)]
    [InlineData("2x0000000000000000000000000000000AA", true)]
    [InlineData("3x0000000000000000000000000000000AA", true)]
    [InlineData("0x4AAAAAAAB1cdEfGhIjKlMnOpQrStUvWxYz", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Reconoce_las_claves_de_prueba_de_Cloudflare(string? clave, bool esPrueba)
        => Assert.Equal(esPrueba, CaptchaOpciones.EsClaveDePrueba(clave));

    [Fact]
    public async Task Deshabilitado_sin_secreto_no_consulta_a_Cloudflare()
    {
        var turnstile = Responde("{\"success\":false}");
        await Crear(turnstile, secreto: "").ExigirAsync(null, AccionesCaptcha.Login);
        Assert.Null(turnstile.CuerpoRecibido);
    }
}
