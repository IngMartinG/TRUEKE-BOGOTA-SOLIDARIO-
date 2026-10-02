using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class PublicacionesController : ControllerBase
{
    private readonly IPublicacionService _pubs;
    private readonly IPagoService _pagos;
    public PublicacionesController(IPublicacionService pubs, IPagoService pagos) { _pubs = pubs; _pagos = pagos; }

    // ---- Públicos (Invitado): solo lectura. Las coordenadas salen aproximadas; las ocultas nunca aparecen.
    [HttpGet("categorias"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> Categorias() => Ok(await _pubs.ListarCategoriasAsync());

    [HttpGet("publicaciones"), AllowAnonymous]
    public async Task<ActionResult<PaginaDto<PublicacionDto>>> Listar([FromQuery] FiltroPublicacionesRequest filtro)
        => Ok(await _pubs.ListarAsync(User.IdActualOpcional(), filtro));

    /// <summary>Publicaciones visibles dentro de un radio (km), ordenadas por distancia (Haversine).</summary>
    [HttpGet("publicaciones/cercanas"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicacionCercanaDto>>> Cercanas([FromQuery] CercanasRequest r)
        => Ok(await _pubs.ListarCercanasAsync(User.IdActualOpcional(), r));

    [HttpGet("publicaciones/{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<PublicacionDto>> Obtener(Guid id) => Ok(await _pubs.ObtenerAsync(User.IdActualOpcional(), id));

    // ---- Requieren sesión
    [HttpGet("publicaciones/mias")]
    public async Task<ActionResult<IReadOnlyList<PublicacionDto>>> Mias() => Ok(await _pubs.ListarMiasAsync(User.IdActual()));

    [HttpPost("publicaciones")]
    public async Task<ActionResult<PublicacionDto>> Crear([FromBody] CrearPublicacionRequest r)
    {
        var creada = await _pubs.CrearAsync(User.IdActual(), r);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPost("publicaciones/{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarPublicacionRequest? r)
    {
        await _pubs.CancelarAsync(User.IdActual(), id, r?.Motivo);
        return NoContent();
    }

    /// <summary>Beneficio Premium: usa uno de los destacados gratis del mes.</summary>
    [HttpPost("publicaciones/{id:guid}/destacar-gratis")]
    public async Task<IActionResult> DestacarGratis(Guid id)
    {
        await _pagos.DestacarGratisAsync(User.IdActual(), id);
        return NoContent();
    }
}
