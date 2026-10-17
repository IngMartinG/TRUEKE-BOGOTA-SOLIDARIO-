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
    private readonly IDeseoService _deseos;
    public PerfilesController(IPublicacionService pubs, ISolicitudService solicitudes, IDeseoService deseos)
    {
        _pubs = pubs; _solicitudes = solicitudes; _deseos = deseos;
    }

    /// <summary>Incluye su lista pública de "Lo que busco".</summary>
    [HttpGet("perfil"), AllowAnonymous]
    public async Task<ActionResult<PerfilUsuarioDto>> Perfil(Guid id)
    {
        var actor = User.IdActualOpcional();
        var perfil = await _pubs.ObtenerPerfilAsync(actor, id); // 404 si no está visible
        return Ok(perfil with { Deseos = await _deseos.VisiblesDePerfilAsync(actor, id) });
    }

    [HttpGet("publicaciones"), AllowAnonymous]
    public async Task<ActionResult<PaginaDto<PublicacionDto>>> Publicaciones(Guid id, [FromQuery] PaginacionRequest p)
        => Ok(await _pubs.ListarDePerfilAsync(User.IdActualOpcional(), id, p.Pagina, p.Tamano));

    [HttpGet("calificaciones"), AllowAnonymous]
    public async Task<ActionResult<PaginaDto<CalificacionDto>>> Calificaciones(Guid id, [FromQuery] PaginacionRequest p)
        => Ok(await _solicitudes.ListarCalificacionesAsync(id, p.Pagina, p.Tamano));
}
