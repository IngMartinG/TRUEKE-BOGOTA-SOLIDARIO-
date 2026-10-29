using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Route("api/v1/impacto")]
[Authorize]
public sealed class ImpactoController : ControllerBase
{
    private readonly IImpactoService _impacto;
    public ImpactoController(IImpactoService impacto) => _impacto = impacto;

    /// <summary>
    /// Público: lo que la comunidad ha hecho circular, con filtros por periodo, modo y localidad (solo totales; cada combinación
    /// se calcula como mucho cada 10 minutos). La localidad debe ser una de LocalidadesDisponibles.
    /// </summary>
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<ImpactoDto>> Obtener([FromQuery] FiltroImpactoRequest filtro) => Ok(await _impacto.ObtenerAsync(filtro));

    /// <summary>Objetos comunes con su peso y material, para ayudar a llenarlos al publicar.</summary>
    [HttpGet("objetos-tipicos")]
    public ActionResult<IReadOnlyList<ObjetoTipicoDto>> ObjetosTipicos() => Ok(_impacto.ObjetosTipicos());
}
