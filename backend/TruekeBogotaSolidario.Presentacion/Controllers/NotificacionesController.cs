using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Bandeja de notificaciones del usuario actual (las mismas que llegan en tiempo real por el hub).</summary>
[ApiController]
[Authorize]
[Route("api/v1/notificaciones")]
public sealed class NotificacionesController : ControllerBase
{
    private readonly INotificacionService _notificaciones;
    public NotificacionesController(INotificacionService notificaciones) => _notificaciones = notificaciones;

    [HttpGet]
    public async Task<ActionResult<PaginaDto<NotificacionDto>>> Listar([FromQuery] FiltroNotificacionesRequest f)
        => Ok(await _notificaciones.ListarAsync(User.IdActual(), f.SoloNoLeidas, f.Pagina, f.Tamano));

    [HttpGet("no-leidas/total")]
    public async Task<ActionResult<int>> TotalNoLeidas() => Ok(await _notificaciones.ContarNoLeidasAsync(User.IdActual()));

    [HttpPost("{id:guid}/leer")]
    public async Task<IActionResult> MarcarLeida(Guid id)
    {
        await _notificaciones.MarcarLeidaAsync(User.IdActual(), id);
        return NoContent();
    }

    [HttpPost("leer-todas")]
    public async Task<IActionResult> MarcarTodas()
    {
        await _notificaciones.MarcarTodasLeidasAsync(User.IdActual());
        return NoContent();
    }
}
