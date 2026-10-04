using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class DenunciasController : ControllerBase
{
    private readonly IDenunciaService _denuncias;
    public DenunciasController(IDenunciaService denuncias) => _denuncias = denuncias;

    /// <summary>Denunciar una publicación, comentario, mensaje (solo participantes) o usuario. Una vez por objetivo.</summary>
    [HttpPost("denuncias"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<DenunciaCreadaDto>> Crear([FromBody] CrearDenunciaRequest r)
        => StatusCode(StatusCodes.Status201Created, await _denuncias.CrearAsync(User.IdActual(), r));

    /// <summary>Denuncias procedentes contra mí: qué se decidió y si aún puedo contar mi versión. Nunca dice quién denunció.</summary>
    [HttpGet("denuncias/recibidas")]
    public async Task<ActionResult<IReadOnlyList<DenunciaRecibidaDto>>> Recibidas()
        => Ok(await _denuncias.ListarRecibidasAsync(User.IdActual()));

    /// <summary>Apelar (dar mi versión) una vez por resolución, dentro del plazo.</summary>
    [HttpPost("denuncias/recibidas/{resolucionId:guid}/apelacion"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<ApelacionDto>> Apelar(Guid resolucionId, [FromBody] CrearApelacionRequest r)
        => StatusCode(StatusCodes.Status201Created, await _denuncias.ApelarAsync(User.IdActual(), resolucionId, r.Texto));

    // ---- Cola de moderación: Administrador y SuperUsuario
    [HttpGet("admin/denuncias"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<IReadOnlyList<DenunciaAgrupadaDto>>> Listar([FromQuery] FiltroDenunciasRequest f)
        => Ok(await _denuncias.ListarAsync(User.IdActual(), f.Estado));

    /// <summary>Resuelve TODAS las denuncias pendientes sobre el mismo objetivo.</summary>
    [HttpPost("admin/denuncias/{id:guid}/resolver"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Resolver(Guid id, [FromBody] ResolverDenunciaRequest r)
    {
        await _denuncias.ResolverAsync(User.IdActual(), id, r.Accion, r.Nota);
        return NoContent();
    }

    [HttpGet("admin/apelaciones"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<IReadOnlyList<ApelacionAdminDto>>> ListarApelaciones([FromQuery] FiltroApelacionesRequest f)
        => Ok(await _denuncias.ListarApelacionesAsync(User.IdActual(), f.Estado));

    /// <summary>Aceptar revierte la medida (el contenido vuelve a verse). Debe resolverla otro moderador o un SuperUsuario.</summary>
    [HttpPost("admin/apelaciones/{id:guid}/resolver"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> ResolverApelacion(Guid id, [FromBody] ResolverApelacionRequest r)
    {
        await _denuncias.ResolverApelacionAsync(User.IdActual(), id, r.Aceptar, r.Nota);
        return NoContent();
    }
}
