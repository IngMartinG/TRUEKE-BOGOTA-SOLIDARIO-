using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El transporte de Azure Communication Services arma el correo igual que el SMTP (remitente, destinatario, texto y HTML).</summary>
public class CorreoAzureTests
{
    /// <summary>El SDK de Azure permite heredar del cliente para pruebas (constructor protegido y métodos virtuales).</summary>
    private sealed class EmailClientFalso : EmailClient
    {
        public EmailMessage? Enviado { get; private set; }
        public WaitUntil? Espera { get; private set; }

        public override Task<EmailSendOperation> SendAsync(WaitUntil wait, EmailMessage message, CancellationToken cancellationToken = default)
        {
            Espera = wait;
            Enviado = message;
            return Task.FromResult<EmailSendOperation>(null!);
        }
    }

    [Fact]
    public async Task Envia_desde_el_remitente_configurado_con_texto_y_html()
    {
        var cliente = new EmailClientFalso();
        var opciones = Options.Create(new CorreoOpciones { Proveedor = CorreoOpciones.ProveedorAzure, Remitente = "DoNotReply@abc.azurecomm.net" });
        var transporte = new TransporteCorreoAzure(cliente, opciones);

        await transporte.EnviarAsync(new MensajeCorreo("persona@ejemplo.co", "Confirma tu correo", "Abre este enlace", "<p>Abre este enlace</p>"), CancellationToken.None);

        var m = Assert.IsType<EmailMessage>(cliente.Enviado);
        Assert.Equal("DoNotReply@abc.azurecomm.net", m.SenderAddress);
        Assert.Equal("persona@ejemplo.co", Assert.Single(m.Recipients.To).Address);
        Assert.Equal("Confirma tu correo", m.Content.Subject);
        Assert.Equal("Abre este enlace", m.Content.PlainText);
        Assert.Equal("<p>Abre este enlace</p>", m.Content.Html);
        Assert.Equal(WaitUntil.Started, cliente.Espera); // no bloquea el worker esperando la entrega
    }

    [Theory]
    [InlineData("AzureCommunication", true)]
    [InlineData("azurecommunication", true)]
    [InlineData("Smtp", false)]
    public void Reconoce_el_proveedor_sin_importar_mayusculas(string proveedor, bool esAzure)
        => Assert.Equal(esAzure, new CorreoOpciones { Proveedor = proveedor }.EsAzure);
}
