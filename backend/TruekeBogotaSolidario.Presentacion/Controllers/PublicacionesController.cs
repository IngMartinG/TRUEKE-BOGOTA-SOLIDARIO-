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

    /// <summary>Vitrina: publicaciones destacadas vigentes (orden aleatorio). El front la muestra sobre el catálogo con cualquier filtro.</summary>
    [HttpGet("publicaciones/destacadas"), AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicacionDto>>> Destacadas([FromQuery] DestacadasRequest r)
        => Ok(await _pubs.ListarDestacadasAsync(User.IdActualOpcional(), r));

    [HttpGet("publicaciones/{id:guid}"), AllowAnonymous]
    public async Task<ActionResult<PublicacionDto>> Obtener(Guid id)
        => Ok(await _pubs.ObtenerAsync(User.IdActualOpcional(), id, HttpContext.VisitanteParaEstadisticas()));

    // ---- Requieren sesión
    [HttpGet("publicaciones/mias")]
    public async Task<ActionResult<IReadOnlyList<PublicacionDto>>> Mias() => Ok(await _pubs.ListarMiasAsync(User.IdActual()));

    [HttpPost("publicaciones"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<PublicacionDto>> Crear([FromBody] CrearPublicacionRequest r)
    {
        var creada = await _pubs.CrearAsync(User.IdActual(), r);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    /// <summary>Editar (solo el dueño y solo si está Disponible). Reemplaza todos los campos, incluida la lista de fotos.</summary>
    [HttpPut("publicaciones/{id:guid}"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<PublicacionDto>> Editar(Guid id, [FromBody] CrearPublicacionRequest r)
        => Ok(await _pubs.EditarAsync(User.IdActual(), id, r));

    // ---- Favoritos (idempotentes)
    [HttpPost("publicaciones/{id:guid}/favorito")]
    public async Task<IActionResult> MarcarFavorita(Guid id)
    {
        await _pubs.MarcarFavoritaAsync(User.IdActual(), id);
        return NoContent();
    }

    [HttpDelete("publicaciones/{id:guid}/favorito")]
    public async Task<IActionResult> QuitarFavorita(Guid id)
    {
        await _pubs.QuitarFavoritaAsync(User.IdActual(), id);
        return NoContent();
    }

    [HttpGet("favoritos")]
    public async Task<ActionResult<PaginaDto<PublicacionDto>>> Favoritos([FromQuery] PaginacionRequest p)
        => Ok(await _pubs.ListarFavoritasAsync(User.IdActual(), p.Pagina, p.Tamano));

    [HttpPost("publicaciones/{id:guid}/cancelar")]
    public async Task<IActionResult> Cancelar(Guid id, [FromBody] CancelarPublicacionRequest? r)
    {
        await _pubs.CancelarAsync(User.IdActual(), id, r?.Motivo);
        return NoContent();
    }

    /// <summary>Beneficio de los planes Premium y Empresa: usa uno de los destacados gratis del mes.</summary>
    [HttpPost("publicaciones/{id:guid}/destacar-gratis")]
    public async Task<IActionResult> DestacarGratis(Guid id)
    {
        await _pagos.DestacarGratisAsync(User.IdActual(), id);
        return NoContent();
    }

    /// <summary>Sube la publicación al primer lugar de "Más recientes" a cambio de Eco-Puntos (una vez cada 24 h).</summary>
    [HttpPost("publicaciones/{id:guid}/impulsar"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<PublicacionDto>> Impulsar(Guid id) => Ok(await _pubs.ImpulsarAsync(User.IdActual(), id));

    /// <summary>Vistas, favoritos y solicitudes (solo el dueño). La serie diaria de 30 días es para planes Premium y Empresa.</summary>
    [HttpGet("publicaciones/{id:guid}/estadisticas")]
    public async Task<ActionResult<EstadisticasPublicacionDto>> Estadisticas(Guid id)
        => Ok(await _pubs.ObtenerEstadisticasAsync(User.IdActual(), id));
}
