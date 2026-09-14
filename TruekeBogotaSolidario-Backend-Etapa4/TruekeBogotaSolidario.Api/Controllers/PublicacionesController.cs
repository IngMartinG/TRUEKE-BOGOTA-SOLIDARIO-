using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Api.DTOs.Publicaciones;
using TruekeBogotaSolidario.Api.Services.Interfaces;

namespace TruekeBogotaSolidario.Api.Controllers;

// ==========================================================================
// CAPA DE CONTROLADOR (equivalente al "ListingController" del C4 - Nivel 3)
// Solo enruta y traduce HTTP <-> DTOs. Toda la logica vive en IPublicacionService.
// Convencion de versionado: /api/v1/publicaciones
// ==========================================================================
[ApiController]
[Route("api/v1/publicaciones")]
[Produces("application/json")]
public class PublicacionesController : ControllerBase
{
    private readonly IPublicacionService _publicacionService;

    public PublicacionesController(IPublicacionService publicacionService)
    {
        _publicacionService = publicacionService;
    }

    /// <summary>Lista todas las publicaciones activas del catalogo de trueke.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<PublicacionResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerTodas()
    {
        var publicaciones = await _publicacionService.ObtenerTodasAsync();
        return Ok(publicaciones);
    }

    /// <summary>Obtiene el detalle de una publicacion por su Id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PublicacionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(Guid id)
    {
        var publicacion = await _publicacionService.ObtenerPorIdAsync(id);
        return Ok(publicacion);
    }

    /// <summary>Publica un nuevo bien para intercambiar.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PublicacionResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearPublicacionDto dto)
    {
        // ModelState valida las DataAnnotations del DTO (campos obligatorios, longitudes, etc.)
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var creada = await _publicacionService.CrearAsync(dto);

        // 201 Created + Location header apuntando al recurso recien creado.
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creada.Id }, creada);
    }

    /// <summary>
    /// Aplica una transicion de estado sobre la publicacion:
    /// en-negociacion | confirmar | cancelar.
    /// </summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType(typeof(PublicacionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] ActualizarEstadoPublicacionDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var actualizada = await _publicacionService.CambiarEstadoAsync(id, dto);
        return Ok(actualizada);
    }
}
