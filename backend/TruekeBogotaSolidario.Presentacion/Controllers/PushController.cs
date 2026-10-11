using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Push;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Dispositivos que reciben notificaciones push de la persona. El navegador entrega la suscripción al aceptar el permiso;
/// el front la registra aquí y la da de baja al desactivarlas o al cerrar sesión en ese dispositivo.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/push/suscripciones")]
public sealed class PushController : ControllerBase
{
    private readonly IPushService _push;
    public PushController(IPushService push) => _push = push;

    /// <summary>Registra (o renueva) este dispositivo. Si el navegador era de otra cuenta, pasa a esta.</summary>
    [HttpPut, EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<IActionResult> Registrar([FromBody] SuscripcionPushRequest r)
    {
        await _push.RegistrarAsync(User.IdActual(), r);
        return NoContent();
    }

    /// <summary>Da de baja este dispositivo (idempotente).</summary>
    [HttpPost("baja"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<IActionResult> Baja([FromBody] BajaPushRequest r)
    {
        await _push.QuitarAsync(User.IdActual(), r.Endpoint);
        return NoContent();
    }
}
