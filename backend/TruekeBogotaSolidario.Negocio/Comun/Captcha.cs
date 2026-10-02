using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

public sealed class CaptchaOpciones
{
    public const string Seccion = "Captcha";
    /// <summary>Clave secreta de reCAPTCHA v3 (servidor). Vacía = deshabilitado (solo fuera de Producción).</summary>
    public string ClaveSecreta { get; set; } = "";
    /// <summary>Clave de sitio (pública): el front la obtiene de GET /api/v1/configuracion.</summary>
    public string ClaveSitio { get; set; } = "";
    /// <summary>reCAPTCHA v3 puntúa de 0.0 (bot) a 1.0 (persona).</summary>
    [Range(0.1, 0.9)] public double PuntajeMinimo { get; set; } = 0.5;
    public string UrlVerificacion { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
    public bool Habilitado => !string.IsNullOrWhiteSpace(ClaveSecreta);
}

/// <summary>Acciones de reCAPTCHA (el front usa el mismo nombre en grecaptcha.execute).</summary>
public static class AccionesCaptcha
{
    public const string Registro = "registro";
    public const string Login = "login";
    public const string OlvideClave = "olvide_clave";
}

/// <summary>Frena bots en registro, login y recuperación de clave (además del rate limiting por IP).</summary>
public interface IVerificadorCaptcha
{
    bool Habilitado { get; }
    /// <summary>Lanza ReglaDeNegocioException (400) si el token no es válido, es de otra acción o el puntaje es bajo.</summary>
    Task ExigirAsync(string? token, string accion, CancellationToken ct = default);
}

public sealed class VerificadorRecaptcha : IVerificadorCaptcha
{
    private const string Rechazo = "No pudimos verificar que eres una persona. Recarga la página e inténtalo de nuevo.";

    private sealed record Respuesta(
        [property: JsonPropertyName("success")] bool Exito,
        [property: JsonPropertyName("score")] double? Puntaje,
        [property: JsonPropertyName("action")] string? Accion,
        [property: JsonPropertyName("error-codes")] string[]? Errores);

    private readonly HttpClient _http;
    private readonly CaptchaOpciones _o;
    private readonly ILogger<VerificadorRecaptcha> _log;

    public VerificadorRecaptcha(HttpClient http, IOptions<CaptchaOpciones> o, ILogger<VerificadorRecaptcha> log)
    {
        _http = http; _o = o.Value; _log = log;
    }

    public bool Habilitado => _o.Habilitado;

    public async Task ExigirAsync(string? token, string accion, CancellationToken ct = default)
    {
        if (!Habilitado) return;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096) throw new ReglaDeNegocioException(Rechazo);

        Respuesta? r;
        try
        {
            using var contenido = new FormUrlEncodedContent(new Dictionary<string, string> { ["secret"] = _o.ClaveSecreta, ["response"] = token });
            using var resp = await _http.PostAsync(_o.UrlVerificacion, contenido, ct);
            resp.EnsureSuccessStatusCode();
            r = await resp.Content.ReadFromJsonAsync<Respuesta>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Falla cerrada: si no se puede verificar, no se deja pasar (el usuario reintenta en unos segundos).
            _log.LogWarning(ex, "No se pudo consultar reCAPTCHA");
            throw new ReglaDeNegocioException(Rechazo);
        }

        if (r is null || !r.Exito || r.Accion != accion || (r.Puntaje ?? 0) < _o.PuntajeMinimo)
        {
            _log.LogInformation("reCAPTCHA rechazado: accion={Accion} esperada={Esperada} puntaje={Puntaje} errores={Errores}",
                r?.Accion, accion, r?.Puntaje, r?.Errores is null ? "" : string.Join(',', r.Errores));
            throw new ReglaDeNegocioException(Rechazo);
        }
    }
}
