using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Departamentos y municipios de Colombia (DANE - DIVIPOLA). Datos públicos de referencia: los usa un Invitado para
/// filtrar el catálogo y cualquiera al registrarse o publicar. Se pueden guardar en caché (no cambian).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/ubicaciones")]
public sealed class UbicacionesController : ControllerBase
{
    private const string Cache = "public, max-age=86400";
    private readonly IUbicacionService _ubicaciones;
    public UbicacionesController(IUbicacionService ubicaciones) => _ubicaciones = ubicaciones;

    [HttpGet("departamentos"), AllowAnonymous]
    public ActionResult<IReadOnlyList<DepartamentoDto>> Departamentos()
    {
        Response.Headers.CacheControl = Cache;
        return Ok(_ubicaciones.Departamentos());
    }

    [HttpGet("departamentos/{codigo:regex(^\\d{{2}}$)}/municipios"), AllowAnonymous]
    public ActionResult<IReadOnlyList<MunicipioDto>> Municipios(string codigo)
    {
        Response.Headers.CacheControl = Cache;
        return Ok(_ubicaciones.MunicipiosDe(codigo));
    }

    /// <summary>Búsqueda por nombre (mínimo 2 letras, sin importar tildes).</summary>
    [HttpGet("municipios"), AllowAnonymous]
    public ActionResult<IReadOnlyList<MunicipioDto>> Buscar([FromQuery] string texto, [FromQuery] int max = 20)
        => Ok(_ubicaciones.Buscar((texto ?? "").Length > 60 ? texto![..60] : texto ?? "", max));

    [HttpGet("municipios/{codigo:regex(^\\d{{5}}$)}"), AllowAnonymous]
    public ActionResult<MunicipioDto> Municipio(string codigo)
    {
        Response.Headers.CacheControl = Cache;
        return Ok(_ubicaciones.Obtener(codigo));
    }
}
