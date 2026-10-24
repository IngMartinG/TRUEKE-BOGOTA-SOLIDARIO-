using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Fundaciones verificadas: el listado y la página de cada una son públicos (solo datos de la organización); la solicitud de
/// verificación y "Lo que necesitamos" son de la propia cuenta. La revisión está en /admin/fundaciones (SuperUsuario).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/fundaciones")]
public sealed class FundacionesController : ControllerBase
{
    private readonly IFundacionService _fundaciones;
    public FundacionesController(IFundacionService fundaciones) => _fundaciones = fundaciones;

    /// <summary>Público: fundaciones verificadas, por nombre.</summary>
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<FundacionResumenDto>>> Listar() => Ok(await _fundaciones.ListarAsync());

    /// <summary>Público: página de una fundación verificada con lo que necesita. id = id de la cuenta.</summary>
    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<FundacionPublicaDto>> Obtener(Guid id) => Ok(await _fundaciones.ObtenerAsync(id));

    /// <summary>Perfil de fundación de la propia cuenta (204 si nunca lo pidió).</summary>
    [HttpGet("mia")]
    public async Task<ActionResult<MiFundacionDto>> Mia()
        => await _fundaciones.MiaAsync(User.IdActual()) is { } f ? Ok(f) : NoContent();

    /// <summary>Pide la verificación con el NIT y el certificado de existencia de la Cámara de Comercio (subido como Documento).</summary>
    [HttpPost("mia"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<MiFundacionDto>> Solicitar([FromBody] SolicitarFundacionRequest r, CancellationToken ct)
        => Ok(await _fundaciones.SolicitarAsync(User.IdActual(), r, ct));

    [HttpPut("mia"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<MiFundacionDto>> Editar([FromBody] EditarFundacionRequest r)
        => Ok(await _fundaciones.EditarAsync(User.IdActual(), r));

    /// <summary>La cuenta deja de ser fundación (se borran el perfil, la insignia y las necesidades).</summary>
    [HttpDelete("mia")]
    public async Task<IActionResult> Eliminar()
    {
        await _fundaciones.EliminarAsync(User.IdActual());
        return NoContent();
    }

    /// <summary>Agrega algo a "Lo que necesitamos" (máximo 10; solo fundaciones verificadas).</summary>
    [HttpPost("mia/necesidades"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<NecesidadDto>> CrearNecesidad([FromBody] GuardarNecesidadRequest r)
    {
        var n = await _fundaciones.CrearNecesidadAsync(User.IdActual(), r);
        return CreatedAtAction(nameof(Mia), null, n);
    }

    [HttpPut("mia/necesidades/{id:guid}"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<NecesidadDto>> ActualizarNecesidad(Guid id, [FromBody] GuardarNecesidadRequest r)
        => Ok(await _fundaciones.ActualizarNecesidadAsync(User.IdActual(), id, r));

    /// <summary>Se borra cuando ya se cubrió.</summary>
    [HttpDelete("mia/necesidades/{id:guid}")]
    public async Task<IActionResult> EliminarNecesidad(Guid id)
    {
        await _fundaciones.EliminarNecesidadAsync(User.IdActual(), id);
        return NoContent();
    }
}
