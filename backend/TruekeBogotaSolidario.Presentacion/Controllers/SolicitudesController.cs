using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Flujo: solicitar → el dueño acepta → coordinan por chat → ambos confirman la entrega → Completada (Eco-Puntos) → calificar.
/// Si el acuerdo no se cumple, cualquiera la marca como no concretada.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/solicitudes")]
public sealed class SolicitudesController : ControllerBase
{
    private readonly ISolicitudService _solicitudes;
    public SolicitudesController(ISolicitudService solicitudes) => _solicitudes = solicitudes;

    [HttpPost, EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<SolicitudDto>> Crear([FromBody] CrearSolicitudRequest r)
        => StatusCode(StatusCodes.Status201Created, await _solicitudes.CrearAsync(User.IdActual(), r));

    [HttpGet("enviadas")]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> Enviadas() => Ok(await _solicitudes.ListarEnviadasAsync(User.IdActual()));

    [HttpGet("recibidas")]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> Recibidas() => Ok(await _solicitudes.ListarRecibidasAsync(User.IdActual()));

    /// <summary>
    /// Acepta la propuesta vigente quien debe responderla (el dueño la solicitud; quien solicita, la contraoferta del dueño):
    /// queda Aceptada para coordinar la entrega (todavía no hay Eco-Puntos).
    /// </summary>
    [HttpPost("{id:guid}/aceptar")]
    public async Task<ActionResult<SolicitudDto>> Aceptar(Guid id) => Ok(await _solicitudes.AceptarAsync(User.IdActual(), id));

    /// <summary>
    /// Contraoferta de quien debe responder: otro precio (Compra) u otros objetos de quien solicita, de 1 a 3 (Trueke).
    /// Pasa el turno a la otra parte; máximo 5 por solicitud. En una donación no hay contraofertas.
    /// </summary>
    [HttpPost("{id:guid}/contraoferta"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<SolicitudDto>> Contraoferta(Guid id, [FromBody] ContraofertaRequest r)
        => Ok(await _solicitudes.ContraofertarAsync(User.IdActual(), id, r));

    [HttpPost("{id:guid}/rechazar")]
    public async Task<IActionResult> Rechazar(Guid id, [FromBody] RechazarSolicitudRequest? r)
    {
        await _solicitudes.RechazarAsync(User.IdActual(), id, r?.Motivo);
        return NoContent();
    }

    [HttpPost("{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        await _solicitudes.CancelarAsync(User.IdActual(), id);
        return NoContent();
    }

    /// <summary>Cada parte confirma que la entrega ocurrió. Con las dos confirmaciones queda Completada y se otorgan los Eco-Puntos.</summary>
    [HttpPost("{id:guid}/confirmar-entrega")]
    public async Task<ActionResult<SolicitudDto>> ConfirmarEntrega(Guid id) => Ok(await _solicitudes.ConfirmarEntregaAsync(User.IdActual(), id));

    /// <summary>El intercambio acordado no ocurrió (solo si nadie confirmó la entrega). La publicación vuelve a estar disponible.</summary>
    [HttpPost("{id:guid}/no-concretada")]
    public async Task<ActionResult<SolicitudDto>> NoConcretada(Guid id, [FromBody] NoConcretadaRequest r)
        => Ok(await _solicitudes.MarcarNoConcretadaAsync(User.IdActual(), id, r.Motivo));

    /// <summary>Calificar a la otra parte (1 a 5 estrellas) dentro de los 30 días siguientes a completar el intercambio.</summary>
    [HttpPost("{id:guid}/calificar"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<CalificacionDto>> Calificar(Guid id, [FromBody] CalificarRequest r)
        => StatusCode(StatusCodes.Status201Created, await _solicitudes.CalificarAsync(User.IdActual(), id, r));
}
