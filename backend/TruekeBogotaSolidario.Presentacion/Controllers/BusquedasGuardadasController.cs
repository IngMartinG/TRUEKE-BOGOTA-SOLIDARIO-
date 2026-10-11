using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Búsquedas del catálogo que la persona guarda, con alertas de publicaciones nuevas.</summary>
[ApiController]
[Authorize]
[Route("api/v1/busquedas-guardadas")]
public sealed class BusquedasGuardadasController : ControllerBase
{
    private readonly IBusquedaGuardadaService _busquedas;
    public BusquedasGuardadasController(IBusquedaGuardadaService busquedas) => _busquedas = busquedas;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusquedaGuardadaDto>>> Listar() => Ok(await _busquedas.ListarAsync(User.IdActual()));

    /// <summary>Guarda la búsqueda (máximo 10). Si ya existía la misma, devuelve esa.</summary>
    [HttpPost, EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<BusquedaGuardadaDto>> Guardar([FromBody] GuardarBusquedaRequest r)
    {
        var b = await _busquedas.GuardarAsync(User.IdActual(), r);
        return CreatedAtAction(nameof(Listar), null, b);
    }

    [HttpPut("{id:guid}/alertas"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<BusquedaGuardadaDto>> Alertas(Guid id, [FromBody] AlertasBusquedaRequest r)
        => Ok(await _busquedas.CambiarAlertasAsync(User.IdActual(), id, r.Activas));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        await _busquedas.EliminarAsync(User.IdActual(), id);
        return NoContent();
    }
}
