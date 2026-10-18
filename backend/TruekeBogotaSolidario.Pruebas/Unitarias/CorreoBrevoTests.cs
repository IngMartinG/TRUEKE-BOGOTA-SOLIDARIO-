using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio;
using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El transporte de Brevo arma el correo igual que el SMTP (remitente, destinatario, texto y HTML) y la clave solo va en la cabecera.</summary>
public class CorreoBrevoTests
{
    private sealed class BrevoSimulado : HttpMessageHandler
    {
        private readonly HttpStatusCode _estado;
        public BrevoSimulado(HttpStatusCode estado = HttpStatusCode.Created) => _estado = estado;
        public HttpRequestMessage? Peticion { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Peticion = request;
            Cuerpo = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_estado) { Content = new StringContent("""{"message":"persona@ejemplo.co no es válido"}""") };
        }
    }

    private sealed class FabricaHttp : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaHttp(HttpMessageHandler handler) => _handler = handler;
        public string? Pedido { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Pedido = name;
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private static TransporteCorreoBrevo Crear(HttpMessageHandler brevo, FabricaHttp? fabrica = null)
        => new(fabrica ?? new FabricaHttp(brevo), Options.Create(new CorreoOpciones
        {
            Proveedor = CorreoOpciones.ProveedorBrevo,
            Remitente = "no-responder@trueke.co",
            NombreRemitente = "Trueke Bogotá Solidario",
            Brevo = { ClaveApi = "xkeysib-clave-de-prueba" }
        }));

    [Fact]
    public async Task Envia_a_la_API_de_Brevo_con_la_clave_y_el_correo_completo()
    {
        var brevo = new BrevoSimulado();
        var fabrica = new FabricaHttp(brevo);

        await Crear(brevo, fabrica).EnviarAsync(
            new MensajeCorreo("persona@ejemplo.co", "Confirma tu correo", "Abre este enlace", "<p>Abre este enlace</p>"), CancellationToken.None);

        var req = Assert.IsType<HttpRequestMessage>(brevo.Peticion);
        Assert.Equal(TransporteCorreoBrevo.ClienteHttp, fabrica.Pedido); // el cliente con timeout y sin redirecciones
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", req.RequestUri!.ToString());
        Assert.Equal("xkeysib-clave-de-prueba", Assert.Single(req.Headers.GetValues("api-key")));
        Assert.DoesNotContain("xkeysib", req.RequestUri.ToString());
        Assert.DoesNotContain("xkeysib", brevo.Cuerpo);

        using var json = JsonDocument.Parse(brevo.Cuerpo!);
        var r = json.RootElement;
        Assert.Equal("no-responder@trueke.co", r.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("Trueke Bogotá Solidario", r.GetProperty("sender").GetProperty("name").GetString());
        var para = Assert.Single(r.GetProperty("to").EnumerateArray().ToList());
        Assert.Equal("persona@ejemplo.co", para.GetProperty("email").GetString());
        Assert.False(para.TryGetProperty("name", out _));
        Assert.Equal("Confirma tu correo", r.GetProperty("subject").GetString());
        Assert.Equal("Abre este enlace", r.GetProperty("textContent").GetString());
        Assert.Equal("<p>Abre este enlace</p>", r.GetProperty("htmlContent").GetString());
    }

    [Fact]
    public async Task Sin_html_solo_envia_el_texto()
    {
        var brevo = new BrevoSimulado();
        await Crear(brevo).EnviarAsync(new MensajeCorreo("persona@ejemplo.co", "Aviso", "Solo texto"), CancellationToken.None);

        using var json = JsonDocument.Parse(brevo.Cuerpo!);
        Assert.False(json.RootElement.TryGetProperty("htmlContent", out _));
        Assert.Equal("Solo texto", json.RootElement.GetProperty("textContent").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]   // remitente sin verificar o datos inválidos
    [InlineData(HttpStatusCode.Unauthorized)] // clave API inválida
    [InlineData(HttpStatusCode.TooManyRequests)] // límite diario del plan gratis
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Si_Brevo_no_acepta_el_correo_lanza_para_que_el_worker_reintente(HttpStatusCode estado)
    {
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            Crear(new BrevoSimulado(estado)).EnviarAsync(new MensajeCorreo("persona@ejemplo.co", "Aviso", "Texto"), CancellationToken.None));

        Assert.Equal(estado, ex.StatusCode);
        Assert.DoesNotContain("persona@ejemplo.co", ex.Message); // la respuesta de Brevo no llega a los logs
    }

    [Theory]
    [InlineData("Brevo", true)]
    [InlineData("brevo", true)]
    [InlineData("Smtp", false)]
    public void Reconoce_el_proveedor_sin_importar_mayusculas(string proveedor, bool esBrevo)
        => Assert.Equal(esBrevo, new CorreoOpciones { Proveedor = proveedor }.EsBrevo);

    [Fact]
    public void Con_Brevo_y_sin_clave_API_la_api_no_arranca()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pagos:Proveedor"] = "Simulado",
            ["Correo:Proveedor"] = "Brevo",
            ["Correo:Brevo:ClaveApi"] = " "
        }).Build();

        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddNegocio(config));
        Assert.Contains("Correo__Brevo__ClaveApi", ex.Message);
    }

    [Fact]
    public void Con_Brevo_y_clave_API_registra_el_transporte_de_Brevo()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pagos:Proveedor"] = "Simulado",
            ["Correo:Proveedor"] = "Brevo",
            ["Correo:Brevo:ClaveApi"] = "xkeysib-clave-de-prueba",
            ["Database:Provider"] = "InMemory",
            ["Database:NombreInMemory"] = "brevo-" + Guid.NewGuid().ToString("N")
        }).Build();

        var servicios = new ServiceCollection().AddNegocio(config);
        Assert.Contains(servicios, d => d.ServiceType == typeof(ITransporteCorreo) && d.ImplementationType == typeof(TransporteCorreoBrevo));
    }
}
