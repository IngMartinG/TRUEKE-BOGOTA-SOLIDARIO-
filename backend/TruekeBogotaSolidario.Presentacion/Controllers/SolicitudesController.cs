using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/solicitudes")]
public sealed class SolicitudesController : ControllerBase
{
    private readonly ISolicitudService _solicitudes;
    public SolicitudesController(ISolicitudService solicitudes) => _solicitudes = solicitudes;

    [HttpPost]
    public async Task<ActionResult<SolicitudDto>> Crear([FromBody] CrearSolicitudRequest r)
        => StatusCode(StatusCodes.Status201Created, await _solicitudes.CrearAsync(User.IdActual(), r));

    [HttpGet("enviadas")]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> Enviadas() => Ok(await _solicitudes.ListarEnviadasAsync(User.IdActual()));

    [HttpGet("recibidas")]
    public async Task<ActionResult<IReadOnlyList<SolicitudDto>>> Recibidas() => Ok(await _solicitudes.ListarRecibidasAsync(User.IdActual()));

    [HttpPost("{id:guid}/aceptar")]
    public async Task<ActionResult<SolicitudDto>> Aceptar(Guid id) => Ok(await _solicitudes.AceptarAsync(User.IdActual(), id));

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
}
