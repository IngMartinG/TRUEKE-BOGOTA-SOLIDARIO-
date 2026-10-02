using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Perfil público de cualquier usuario (lo abre el front desde publicacion.propietario.id). Sin correo ni datos privados.</summary>
[ApiController]
[Authorize]
[Route("api/v1/usuarios/{id:guid}")]
public sealed class PerfilesController : ControllerBase
{
    private readonly IPublicacionService _pubs;
    private readonly ISolicitudService _solicitudes;
    public PerfilesController(IPublicacionService pubs, ISolicitudService solicitudes) { _pubs = pubs; _solicitudes = solicitudes; }

    [HttpGet("perfil"), AllowAnonymous]
    public async Task<ActionResult<PerfilUsuarioDto>> Perfil(Guid id) => Ok(await _pubs.ObtenerPerfilAsync(id));

    [HttpGet("publicaciones"), AllowAnonymous]
    public async Task<ActionResult<PaginaDto<PublicacionDto>>> Publicaciones(Guid id, [FromQuery] PaginacionRequest p)
        => Ok(await _pubs.ListarDePerfilAsync(User.IdActualOpcional(), id, p.Pagina, p.Tamano));

    [HttpGet("calificaciones"), AllowAnonymous]
    public async Task<ActionResult<PaginaDto<CalificacionDto>>> Calificaciones(Guid id, [FromQuery] PaginacionRequest p)
        => Ok(await _solicitudes.ListarCalificacionesAsync(id, p.Pagina, p.Tamano));
}
