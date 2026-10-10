using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TruekeBogotaSolidario.Negocio.Comun;

public sealed class GoogleOpciones
{
    public const string Seccion = "Google";
    /// <summary>OAuth Client ID (tipo "Aplicación web") de Google Cloud. Vacío = inicio de sesión con Google deshabilitado.</summary>
    public string ClientId { get; set; } = "";
}

public sealed record IdentidadGoogle(string Sub, string Correo, bool CorreoVerificado, string? Nombre, string? Foto = null);

/// <summary>Valida el ID token que Angular obtiene con Google Identity Services. El backend NUNCA confía en datos de Google enviados sueltos.</summary>
public interface IValidadorGoogle
{
    bool Habilitado { get; }
    /// <returns>null si el token no es válido (firma, emisor, audiencia o vigencia).</returns>
    Task<IdentidadGoogle?> ValidarAsync(string idToken);
}

public sealed class ValidadorGoogle : IValidadorGoogle
{
    private readonly GoogleOpciones _o;
    private readonly ILogger<ValidadorGoogle> _log;

    public ValidadorGoogle(IOptions<GoogleOpciones> o, ILogger<ValidadorGoogle> log)
    {
        _o = o.Value;
        _log = log;
    }

    public bool Habilitado => !string.IsNullOrWhiteSpace(_o.ClientId);

    public async Task<IdentidadGoogle?> ValidarAsync(string idToken)
    {
        try
        {
            // Verifica la firma con las llaves públicas de Google, el emisor (accounts.google.com), la vigencia y que el
            // token fue emitido PARA nuestra aplicación (audiencia = nuestro Client ID).
            var p = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _o.ClientId },
                IssuedAtClockTolerance = TimeSpan.FromSeconds(30),
                ExpirationTimeClockTolerance = TimeSpan.FromSeconds(30)
            });
            return string.IsNullOrEmpty(p.Subject) || string.IsNullOrEmpty(p.Email)
                ? null
                : new IdentidadGoogle(p.Subject, p.Email, p.EmailVerified, p.Name, p.Picture);
        }
        catch (InvalidJwtException ex)
        {
            _log.LogInformation("ID token de Google rechazado: {Motivo}", ex.Message);
            return null;
        }
    }
}
