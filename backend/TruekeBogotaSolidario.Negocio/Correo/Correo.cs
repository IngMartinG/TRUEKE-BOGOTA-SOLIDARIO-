using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;
using Azure;
using Azure.Communication.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TruekeBogotaSolidario.Negocio.Correo;

/// <summary>Texto plano siempre; Html opcional (se envían ambos como multipart/alternative).</summary>
public sealed record MensajeCorreo(string Para, string Asunto, string Texto, string? Html = null);

public sealed class SmtpOpciones
{
    public string Host { get; set; } = "";
    [Range(1, 65535)] public int Puerto { get; set; } = 587;
    public string Usuario { get; set; } = "";
    public string Clave { get; set; } = "";
    /// <summary>StartTls (587), SslOnConnect (465). Nunca sin cifrado.</summary>
    public string Seguridad { get; set; } = "StartTls";
}

/// <summary>Azure Communication Services Email: API HTTPS (sirve donde el SMTP está bloqueado, como Render gratis).</summary>
public sealed class AzureComunicacionOpciones
{
    /// <summary>Recurso de Communication Services → Claves → Cadena de conexión (endpoint=...;accesskey=...).</summary>
    public string CadenaConexion { get; set; } = "";
}

public sealed class CorreoOpciones
{
    public const string Seccion = "Correo";
    public const string ProveedorAzure = "AzureCommunication";
    /// <summary>"Smtp", "AzureCommunication" (reales) o "Simulado" (solo desarrollo/pruebas; el arranque lo rechaza en Producción).</summary>
    public string Proveedor { get; set; } = "Smtp";
    /// <summary>Con AzureCommunication debe ser una dirección "MailFrom" del dominio conectado (p. ej. DoNotReply@xxxx.azurecomm.net).</summary>
    [EmailAddress] public string Remitente { get; set; } = "no-responder@trueke.co";
    [StringLength(80)] public string NombreRemitente { get; set; } = "Trueke Bogotá Solidario";
    public SmtpOpciones Smtp { get; set; } = new();
    public AzureComunicacionOpciones Azure { get; set; } = new();
    public bool EsSimulado => string.Equals(Proveedor, "Simulado", StringComparison.OrdinalIgnoreCase);
    public bool EsAzure => string.Equals(Proveedor, ProveedorAzure, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Envío asíncrono: los servicios solo ENCOLAN y responden de inmediato. Así una petición nunca tarda más (ni revela nada)
/// según si se envió un correo o no, y una caída del SMTP no rompe el registro.
/// </summary>
public interface ICorreoSaliente
{
    void Encolar(MensajeCorreo mensaje);
}

public interface ITransporteCorreo
{
    Task EnviarAsync(MensajeCorreo mensaje, CancellationToken ct);
}

public sealed class ColaCorreo : ICorreoSaliente
{
    private readonly Channel<MensajeCorreo> _canal = Channel.CreateBounded<MensajeCorreo>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly ILogger<ColaCorreo> _log;
    public ColaCorreo(ILogger<ColaCorreo> log) => _log = log;

    public ChannelReader<MensajeCorreo> Lector => _canal.Reader;

    public void Encolar(MensajeCorreo mensaje)
    {
        if (!_canal.Writer.TryWrite(mensaje)) _log.LogError("Cola de correo llena: se descartó un mensaje ({Asunto})", mensaje.Asunto);
    }
}

public sealed class EnvioCorreosHostedService : BackgroundService
{
    private readonly ColaCorreo _cola;
    private readonly IServiceScopeFactory _fabrica;
    private readonly ILogger<EnvioCorreosHostedService> _log;

    public EnvioCorreosHostedService(ColaCorreo cola, IServiceScopeFactory fabrica, ILogger<EnvioCorreosHostedService> log)
    {
        _cola = cola; _fabrica = fabrica; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var m in _cola.Lector.ReadAllAsync(ct))
        {
            for (var intento = 1; intento <= 3; intento++)
            {
                try
                {
                    using var scope = _fabrica.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ITransporteCorreo>().EnviarAsync(m, ct);
                    break;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
                catch (Exception ex)
                {
                    // Nunca se registra el destinatario ni el cuerpo (contienen datos personales y enlaces de un solo uso)
                    _log.LogWarning(ex, "Fallo enviando correo ({Asunto}), intento {Intento}/3", m.Asunto, intento);
                    if (intento < 3) await Task.Delay(TimeSpan.FromSeconds(5 * intento), ct);
                }
            }
        }
    }
}

/// <summary>Solo desarrollo: escribe el correo en el log para poder copiar el enlace. Prohibido en Producción.</summary>
public sealed class TransporteCorreoSimulado : ITransporteCorreo
{
    private readonly ILogger<TransporteCorreoSimulado> _log;
    public TransporteCorreoSimulado(ILogger<TransporteCorreoSimulado> log) => _log = log;

    public Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        _log.LogInformation("[CORREO SIMULADO] Para: {Para} | Asunto: {Asunto}\n{Texto}", m.Para, m.Asunto, m.Texto);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Azure Communication Services Email por su API HTTPS. Se usa donde el proveedor bloquea los puertos SMTP (Render gratis).
/// El nombre visible del remitente se configura en Azure (dirección MailFrom del dominio), no aquí.
/// </summary>
public sealed class TransporteCorreoAzure : ITransporteCorreo
{
    private readonly EmailClient _cliente;
    private readonly CorreoOpciones _o;

    public TransporteCorreoAzure(EmailClient cliente, IOptions<CorreoOpciones> o)
    {
        _cliente = cliente; _o = o.Value;
    }

    public async Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        var contenido = new EmailContent(m.Asunto) { PlainText = m.Texto, Html = m.Html };
        var mensaje = new EmailMessage(_o.Remitente, new EmailRecipients(new List<EmailAddress> { new(m.Para) }), contenido);
        // WaitUntil.Started: Azure aceptó el correo y lo entrega en segundo plano (no se bloquea el worker esperando).
        // Si falla (credencial, remitente no conectado, límite por hora), lanza y el worker reintenta.
        await _cliente.SendAsync(WaitUntil.Started, mensaje, ct);
    }
}

/// <summary>SMTP con TLS obligatorio (sirve para Azure Communication Services, SendGrid, Gmail, etc.).</summary>
public sealed class TransporteCorreoSmtp : ITransporteCorreo
{
    private readonly CorreoOpciones _o;
    public TransporteCorreoSmtp(IOptions<CorreoOpciones> o) => _o = o.Value;

    public async Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(_o.NombreRemitente, _o.Remitente));
        mensaje.To.Add(MailboxAddress.Parse(m.Para));
        mensaje.Subject = m.Asunto;
        mensaje.Body = new BodyBuilder { TextBody = m.Texto, HtmlBody = m.Html }.ToMessageBody();

        using var cliente = new SmtpClient { Timeout = 15_000 };
        var seguridad = string.Equals(_o.Smtp.Seguridad, "SslOnConnect", StringComparison.OrdinalIgnoreCase)
            ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await cliente.ConnectAsync(_o.Smtp.Host, _o.Smtp.Puerto, seguridad, ct);
        if (!string.IsNullOrEmpty(_o.Smtp.Usuario)) await cliente.AuthenticateAsync(_o.Smtp.Usuario, _o.Smtp.Clave, ct);
        await cliente.SendAsync(mensaje, ct);
        await cliente.DisconnectAsync(true, ct);
    }
}
