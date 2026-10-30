using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Negocios con el plan Negocio vigente: el directorio y la vitrina de cada uno son públicos (solo datos del negocio; autorizado
/// por el dueño el 2026-10-29). Su catálogo es GET /publicaciones?propietarioId=…; las estadísticas de la cuenta, de la propia
/// cuenta. La revisión del NIT está en /admin/negocios.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/negocios")]
public sealed class NegociosController : ControllerBase
{
    private readonly INegocioService _negocios;
    public NegociosController(INegocioService negocios) => _negocios = negocios;

    /// <summary>Público: negocios vigentes, los verificados primero (en caché 2 minutos).</summary>
    [HttpGet, AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<NegocioResumenDto>>> Listar() => Ok(await _negocios.ListarAsync());

    /// <summary>Público: vitrina de un negocio. id = id de la cuenta.</summary>
    [HttpGet("{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<NegocioPublicoDto>> Obtener(Guid id) => Ok(await _negocios.ObtenerAsync(id));

    /// <summary>Planes pagos: rendimiento de toda la cuenta en los últimos 30 días.</summary>
    [HttpGet("mio/estadisticas")]
    public async Task<ActionResult<EstadisticasCuentaDto>> Estadisticas() => Ok(await _negocios.EstadisticasCuentaAsync(User.IdActual()));
}
