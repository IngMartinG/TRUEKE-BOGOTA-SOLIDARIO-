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

    /// <summary>Público: lo que la comunidad ha hecho circular (solo totales, calculados como mucho cada 10 minutos).</summary>
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<ImpactoDto>> Obtener() => Ok(await _impacto.ObtenerAsync());
}
