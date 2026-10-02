using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El verificador real contra una respuesta simulada de Google siteverify.</summary>
public class CaptchaTests
{
    private sealed class GoogleSimulado : HttpMessageHandler
    {
        private readonly Func<string, (HttpStatusCode, string)> _responder;
        public string? CuerpoRecibido { get; private set; }
        public GoogleSimulado(Func<string, (HttpStatusCode, string)> responder) => _responder = responder;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            CuerpoRecibido = await request.Content!.ReadAsStringAsync(ct);
            var (estado, json) = _responder(CuerpoRecibido);
            return new HttpResponseMessage(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private static VerificadorRecaptcha Crear(GoogleSimulado google, string secreto = "secreto-servidor")
        => new(new HttpClient(google), Options.Create(new CaptchaOpciones { ClaveSecreta = secreto, ClaveSitio = "sitio" }),
            NullLogger<VerificadorRecaptcha>.Instance);

    private static GoogleSimulado Responde(string json) => new(_ => (HttpStatusCode.OK, json));

    [Fact]
    public async Task Persona_con_buen_puntaje_y_accion_correcta_pasa_y_se_envia_el_secreto()
    {
        var google = Responde("{\"success\":true,\"score\":0.9,\"action\":\"registro\"}");
        await Crear(google).ExigirAsync("token-del-navegador", AccionesCaptcha.Registro);
        Assert.Contains("secret=secreto-servidor", google.CuerpoRecibido);
        Assert.Contains("response=token-del-navegador", google.CuerpoRecibido);
    }

    [Theory]
    [InlineData("{\"success\":true,\"score\":0.2,\"action\":\"registro\"}")]        // probable bot
    [InlineData("{\"success\":true,\"score\":0.9,\"action\":\"login\"}")]           // token de otra acción (reutilizado)
    [InlineData("{\"success\":false,\"error-codes\":[\"timeout-or-duplicate\"]}")]  // vencido o ya usado
    [InlineData("no es json")]
    public async Task Rechaza_bots_tokens_de_otra_accion_vencidos_o_respuestas_raras(string json)
        => await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Crear(Responde(json)).ExigirAsync("t", AccionesCaptcha.Registro));

    [Fact]
    public async Task Sin_token_o_si_Google_no_responde_falla_cerrado()
    {
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => Crear(Responde("{}")).ExigirAsync(null, AccionesCaptcha.Login));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            Crear(new GoogleSimulado(_ => (HttpStatusCode.ServiceUnavailable, ""))).ExigirAsync("t", AccionesCaptcha.Login));
    }

    [Fact]
    public async Task Deshabilitado_sin_secreto_no_consulta_a_Google()
    {
        var google = Responde("{\"success\":false}");
        await Crear(google, secreto: "").ExigirAsync(null, AccionesCaptcha.Login);
        Assert.Null(google.CuerpoRecibido);
    }
}
