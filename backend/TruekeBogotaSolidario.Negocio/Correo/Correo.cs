using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
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

/// <summary>Brevo: API HTTPS de correo transaccional (sirve donde el SMTP está bloqueado, como Render gratis).</summary>
public sealed class BrevoOpciones
{
    /// <summary>Brevo → SMTP y API → Claves API (xkeysib-...). Es un secreto: nunca va en appsettings ni en el repo.</summary>
    public string ClaveApi { get; set; } = "";
}

public sealed class CorreoOpciones
{
    public const string Seccion = "Correo";
    public const string ProveedorBrevo = "Brevo";
    /// <summary>"Smtp", "Brevo" (reales) o "Simulado" (solo desarrollo/pruebas; el arranque lo rechaza en Producción).</summary>
    public string Proveedor { get; set; } = "Smtp";
    /// <summary>Con Brevo debe ser un remitente verificado en la cuenta (Brevo → Remitentes); si no, Brevo rechaza el envío.</summary>
    [EmailAddress] public string Remitente { get; set; } = "no-responder@trueke.co";
    [StringLength(80)] public string NombreRemitente { get; set; } = "Trueke Bogotá Solidario";
    public SmtpOpciones Smtp { get; set; } = new();
    public BrevoOpciones Brevo { get; set; } = new();
    public bool EsSimulado => string.Equals(Proveedor, "Simulado", StringComparison.OrdinalIgnoreCase);
    public bool EsBrevo => string.Equals(Proveedor, ProveedorBrevo, StringComparison.OrdinalIgnoreCase);
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
/// Brevo por su API HTTPS (POST /v3/smtp/email). Se usa donde el proveedor bloquea los puertos SMTP (Render gratis).
/// Si Brevo no acepta el correo (clave inválida, remitente sin verificar, límite diario), lanza y el worker reintenta.
/// </summary>
public sealed class TransporteCorreoBrevo : ITransporteCorreo
{
    public const string ClienteHttp = "correo-brevo";
    public static readonly Uri Endpoint = new("https://api.brevo.com/v3/smtp/email");

    private readonly IHttpClientFactory _http;
    private readonly CorreoOpciones _o;

    public TransporteCorreoBrevo(IHttpClientFactory http, IOptions<CorreoOpciones> o)
    {
        _http = http; _o = o.Value;
    }

    public async Task EnviarAsync(MensajeCorreo m, CancellationToken ct)
    {
        var cuerpo = new CorreoBrevo(
            new DireccionBrevo(_o.Remitente, _o.NombreRemitente), [new DireccionBrevo(m.Para)], m.Asunto, m.Texto, m.Html);
        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(cuerpo) };
        req.Headers.Add("api-key", _o.Brevo.ClaveApi);
        req.Headers.Accept.ParseAdd("application/json");

        using var respuesta = await _http.CreateClient(ClienteHttp).SendAsync(req, ct);
        // Solo el código: la respuesta de Brevo puede repetir el destinatario (dato personal).
        if (!respuesta.IsSuccessStatusCode)
            throw new HttpRequestException($"Brevo rechazó el correo (HTTP {(int)respuesta.StatusCode}).", null, respuesta.StatusCode);
    }

    private sealed record DireccionBrevo(
        [property: JsonPropertyName("email")] string Correo,
        [property: JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Nombre = null);

    private sealed record CorreoBrevo(
        [property: JsonPropertyName("sender")] DireccionBrevo Remitente,
        [property: JsonPropertyName("to")] DireccionBrevo[] Para,
        [property: JsonPropertyName("subject")] string Asunto,
        [property: JsonPropertyName("textContent")] string Texto,
        [property: JsonPropertyName("htmlContent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Html);
}

/// <summary>SMTP con TLS obligatorio (sirve para Brevo, SendGrid, Gmail, etc. donde el puerto no esté bloqueado).</summary>
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
