using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Api.DTOs.Solicitudes;
using TruekeBogotaSolidario.Api.Services.Interfaces;

namespace TruekeBogotaSolidario.Api.Controllers;

// ==========================================================================
// CAPA DE CONTROLADOR
// Expone el flujo central del negocio: solicitar, aceptar y rechazar un
// trueke. Delega toda la orquestacion en ITruequeService.
// ==========================================================================
[ApiController]
[Route("api/v1/solicitudes")]
[Produces("application/json")]
public class SolicitudesController : ControllerBase
{
    private readonly ITruequeService _truequeService;

    public SolicitudesController(ITruequeService truequeService)
    {
        _truequeService = truequeService;
    }

    /// <summary>Lista las solicitudes recibidas por una publicacion.</summary>
    [HttpGet("por-publicacion/{publicacionId:guid}")]
    [ProducesResponseType(typeof(List<SolicitudResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerPorPublicacion(Guid publicacionId)
    {
        return Ok(await _truequeService.ObtenerPorPublicacionAsync(publicacionId));
    }

    /// <summary>Un usuario solicita el intercambio de una publicacion disponible.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SolicitudResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Solicitar([FromBody] CrearSolicitudDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var creada = await _truequeService.SolicitarIntercambioAsync(dto);
        return CreatedAtAction(nameof(ObtenerPorPublicacion), new { publicacionId = creada.PublicacionId }, creada);
    }

    /// <summary>El dueno de la publicacion acepta la solicitud: confirma el intercambio.</summary>
    [HttpPost("{id:guid}/aceptar")]
    [ProducesResponseType(typeof(SolicitudResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Aceptar(Guid id)
    {
        return Ok(await _truequeService.AceptarAsync(id));
    }

    /// <summary>El dueno de la publicacion rechaza la solicitud, indicando un motivo.</summary>
    [HttpPost("{id:guid}/rechazar")]
    [ProducesResponseType(typeof(SolicitudResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rechazar(Guid id, [FromBody] RechazarSolicitudDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _truequeService.RechazarAsync(id, dto));
    }
}
