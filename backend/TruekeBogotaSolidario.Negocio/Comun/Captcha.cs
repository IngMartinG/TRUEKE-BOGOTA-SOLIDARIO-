using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

public sealed class CaptchaOpciones
{
    public const string Seccion = "Captcha";
    /// <summary>Clave secreta de Cloudflare Turnstile (servidor). Vacía = deshabilitado (solo fuera de Producción).</summary>
    public string ClaveSecreta { get; set; } = "";
    /// <summary>Clave de sitio (pública): el front la obtiene de GET /api/v1/configuracion.</summary>
    public string ClaveSitio { get; set; } = "";
    public string UrlVerificacion { get; set; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    public bool Habilitado => !string.IsNullOrWhiteSpace(ClaveSecreta);

    /// <summary>Claves secretas de prueba de Cloudflare (1x…, 2x…, 3x… seguidas de ceros): no sirven en Producción.</summary>
    public static bool EsClaveDePrueba(string? clave)
        => clave is { Length: > 3 } && clave[1] == 'x' && clave.Contains("x0000000000000000000000000000000", StringComparison.Ordinal);
}

/// <summary>Acciones del captcha (el front usa el mismo nombre en la opción action del widget).</summary>
public static class AccionesCaptcha
{
    public const string Registro = "registro";
    public const string Login = "login";
    public const string OlvideClave = "olvide_clave";
    public const string Pqr = "pqr";
}

/// <summary>Frena bots en registro, login y recuperación de clave (además del rate limiting por IP).</summary>
public interface IVerificadorCaptcha
{
    bool Habilitado { get; }
    /// <summary>Lanza ReglaDeNegocioException (400) si el token no es válido, ya se usó o es de otra acción.</summary>
    Task ExigirAsync(string? token, string accion, CancellationToken ct = default);
}

/// <summary>Cloudflare Turnstile: sin cookies de rastreo y sin puntaje; Cloudflare decide si es persona.</summary>
public sealed class VerificadorTurnstile : IVerificadorCaptcha
{
    private const string Rechazo = "No pudimos verificar que eres una persona. Recarga la página e inténtalo de nuevo.";

    private sealed record Respuesta(
        [property: JsonPropertyName("success")] bool Exito,
        [property: JsonPropertyName("action")] string? Accion,
        [property: JsonPropertyName("error-codes")] string[]? Errores);

    private readonly HttpClient _http;
    private readonly CaptchaOpciones _o;
    private readonly ILogger<VerificadorTurnstile> _log;

    public VerificadorTurnstile(HttpClient http, IOptions<CaptchaOpciones> o, ILogger<VerificadorTurnstile> log)
    {
        _http = http; _o = o.Value; _log = log;
    }

    public bool Habilitado => _o.Habilitado;

    public async Task ExigirAsync(string? token, string accion, CancellationToken ct = default)
    {
        if (!Habilitado) return;
        // Turnstile emite tokens de hasta 2048 caracteres.
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) throw new ReglaDeNegocioException(Rechazo);

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
            _log.LogWarning(ex, "No se pudo consultar Turnstile");
            throw new ReglaDeNegocioException(Rechazo);
        }

        // Con las claves de prueba de Cloudflare la respuesta no trae la acción; con las reales siempre debe coincidir.
        var accionValida = r?.Accion == accion || (r?.Accion is null && CaptchaOpciones.EsClaveDePrueba(_o.ClaveSecreta));
        if (r is null || !r.Exito || !accionValida)
        {
            _log.LogInformation("Turnstile rechazado: accion={Accion} esperada={Esperada} errores={Errores}",
                r?.Accion, accion, r?.Errores is null ? "" : string.Join(',', r.Errores));
            throw new ReglaDeNegocioException(Rechazo);
        }
    }
}
