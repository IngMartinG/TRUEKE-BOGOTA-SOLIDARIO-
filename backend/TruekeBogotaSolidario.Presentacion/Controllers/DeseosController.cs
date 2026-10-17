using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>"Lo que busco": la lista de deseos de la persona y las publicaciones de otras que coinciden.</summary>
[ApiController]
[Authorize]
[Route("api/v1/deseos")]
public sealed class DeseosController : ControllerBase
{
    private readonly IDeseoService _deseos;
    public DeseosController(IDeseoService deseos) => _deseos = deseos;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeseoDto>>> Listar() => Ok(await _deseos.ListarAsync(User.IdActual()));

    /// <summary>Agrega un deseo (máximo 10).</summary>
    [HttpPost, EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<DeseoDto>> Crear([FromBody] GuardarDeseoRequest r)
    {
        var d = await _deseos.CrearAsync(User.IdActual(), r);
        return CreatedAtAction(nameof(Listar), null, d);
    }

    [HttpPut("{id:guid}"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<DeseoDto>> Actualizar(Guid id, [FromBody] GuardarDeseoRequest r)
        => Ok(await _deseos.ActualizarAsync(User.IdActual(), id, r));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Eliminar(Guid id)
    {
        await _deseos.EliminarAsync(User.IdActual(), id);
        return NoContent();
    }

    /// <summary>Publicaciones de otras personas que coinciden con tu lista; primero los truekes en los dos sentidos.</summary>
    [HttpGet("coincidencias")]
    public async Task<ActionResult<IReadOnlyList<CoincidenciaDeseoDto>>> Coincidencias()
        => Ok(await _deseos.CoincidenciasAsync(User.IdActual()));
}
