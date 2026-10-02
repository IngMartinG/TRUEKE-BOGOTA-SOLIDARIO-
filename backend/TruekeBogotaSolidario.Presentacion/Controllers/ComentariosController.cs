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
public sealed class ComentariosController : ControllerBase
{
    private readonly IComentarioService _comentarios;
    public ComentariosController(IComentarioService comentarios) => _comentarios = comentarios;

    /// <summary>Público: los comentarios ocultos por moderación nunca aparecen (salvo para moderadores, marcados).</summary>
    [HttpGet("publicaciones/{id:guid}/comentarios"), AllowAnonymous]
    public async Task<ActionResult<PaginaDto<ComentarioDto>>> Listar(Guid id, [FromQuery] PaginacionRequest p)
        => Ok(await _comentarios.ListarAsync(User.IdActualOpcional(), id, p.Pagina, p.Tamano));

    [HttpPost("publicaciones/{id:guid}/comentarios"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<ComentarioDto>> Crear(Guid id, [FromBody] CrearComentarioRequest r)
        => StatusCode(StatusCodes.Status201Created, await _comentarios.CrearAsync(User.IdActual(), id, r));

    // ---- Moderación: Administrador y SuperUsuario
    [HttpPost("admin/comentarios/{id:guid}/ocultar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Ocultar(Guid id, [FromBody] MotivoRequest r)
    {
        await _comentarios.OcultarAsync(User.IdActual(), id, r.Motivo);
        return NoContent();
    }

    [HttpPost("admin/comentarios/{id:guid}/mostrar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Mostrar(Guid id)
    {
        await _comentarios.MostrarAsync(User.IdActual(), id);
        return NoContent();
    }
}
