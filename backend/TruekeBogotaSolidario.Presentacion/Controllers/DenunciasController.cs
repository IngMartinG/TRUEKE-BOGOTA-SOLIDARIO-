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
}
