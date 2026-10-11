using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Volver a demostrar identidad antes de una acción que da control duradero de la cuenta (activar 2FA, crear o cambiar la
/// contraseña, eliminar la cuenta). Con un token de acceso robado (p. ej. por XSS) o un equipo desatendido no basta:
/// - cuentas con contraseña: la contraseña actual; cada intento fallido cuenta para el mismo bloqueo del login;
/// - cuentas solo de Google: un ID token de Google de esa misma cuenta, emitido hace pocos minutos.
/// </summary>
public sealed class ConfirmadorIdentidad
{
    /// <summary>Antigüedad máxima del ID token de Google (Google los emite con 1 hora de vigencia).</summary>
    public static readonly TimeSpan VigenciaGoogle = TimeSpan.FromMinutes(10);

    public const string MensajeConfirmar = "Confirma tu identidad con tu contraseña o volviendo a iniciar sesión con Google.";

    private readonly IValidadorGoogle _google;
    private readonly IUnidadDeTrabajo _uow;
    private readonly SeguridadOpciones _seg;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ConfirmadorIdentidad> _log;

    public ConfirmadorIdentidad(IValidadorGoogle google, IUnidadDeTrabajo uow, IOptions<SeguridadOpciones> seg, TimeProvider reloj,
        ILogger<ConfirmadorIdentidad> log)
    {
        _google = google; _uow = uow; _seg = seg.Value; _reloj = reloj; _log = log;
    }

    public async Task ExigirAsync(Usuario u, string? clave, string? googleIdToken)
    {
        var ahora = _reloj.GetUtcNow();
        if (u.EstaBloqueado(ahora.UtcDateTime))
            throw new ReglaDeNegocioException("Demasiados intentos fallidos. Espera unos minutos e inténtalo de nuevo.");

        if (u.TieneClave)
        {
            if (!string.IsNullOrEmpty(clave) && u.VerificarClave(clave)) return;
            u.RegistrarLoginFallido(_seg.MaxIntentosLogin, TimeSpan.FromMinutes(_seg.MinutosBloqueo), ahora.UtcDateTime);
            try { await _uow.GuardarCambiosAsync(); }
            catch (ConflictoDeConcurrenciaException) { /* otro intento simultáneo ya actualizó el contador */ }
            _log.LogWarning("Contraseña incorrecta al confirmar identidad {UsuarioId}", u.Id);
            throw new ReglaDeNegocioException("La contraseña no es correcta.");
        }

        if (u.GoogleSub is not null && !string.IsNullOrEmpty(googleIdToken)
            && await _google.ValidarAsync(googleIdToken) is { } id
            && id.Sub == u.GoogleSub
            && id.EmitidoUtc is { } emitido && ahora - emitido <= VigenciaGoogle)
            return;
        throw new ReglaDeNegocioException(MensajeConfirmar);
    }
}
