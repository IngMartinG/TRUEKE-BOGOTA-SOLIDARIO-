using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize(Policy = Politicas.Moderador)]
[Route("api/v1/admin")]
public sealed class AdministracionController : ControllerBase
{
    private readonly IAdministracionService _admin;
    public AdministracionController(IAdministracionService admin) => _admin = admin;

    // ---- Moderación: Administrador y SuperUsuario
    [HttpPost("publicaciones/{id:guid}/ocultar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Ocultar(Guid id, [FromBody] MotivoRequest r)
    {
        await _admin.OcultarPublicacionAsync(User.IdActual(), id, r.Motivo);
        return NoContent();
    }

    [HttpPost("publicaciones/{id:guid}/mostrar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Mostrar(Guid id)
    {
        await _admin.MostrarPublicacionAsync(User.IdActual(), id);
        return NoContent();
    }

    [HttpGet("verificaciones"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<IReadOnlyList<VerificacionPendienteDto>>> Verificaciones()
        => Ok(await _admin.ListarVerificacionesPendientesAsync(User.IdActual()));

    [HttpPost("verificaciones/{usuarioId:guid}/aprobar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Aprobar(Guid usuarioId)
    {
        await _admin.AprobarVerificacionAsync(User.IdActual(), usuarioId);
        return NoContent();
    }

    [HttpPost("verificaciones/{usuarioId:guid}/rechazar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> Rechazar(Guid usuarioId, [FromBody] MotivoRequest r)
    {
        await _admin.RechazarVerificacionAsync(User.IdActual(), usuarioId, r.Motivo);
        return NoContent();
    }

    // ---- Cuentas
    [HttpGet("usuarios"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PaginaDto<UsuarioAdminDto>>> BuscarUsuarios([FromQuery] BuscarUsuariosRequest r)
        => Ok(await _admin.BuscarUsuariosAsync(User.IdActual(), r));

    /// <summary>Suspende (dias = null → indefinida): cierra sus sesiones y oculta su contenido del catálogo.</summary>
    [HttpPost("usuarios/{usuarioId:guid}/suspender"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<UsuarioAdminDto>> Suspender(Guid usuarioId, [FromBody] SuspenderUsuarioRequest r)
        => Ok(await _admin.SuspenderAsync(User.IdActual(), usuarioId, r.Motivo, r.Dias));

    [HttpPost("usuarios/{usuarioId:guid}/reactivar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<UsuarioAdminDto>> Reactivar(Guid usuarioId) => Ok(await _admin.ReactivarAsync(User.IdActual(), usuarioId));

    // ---- Pagos (p. ej. los cobrados cuyo beneficio no se pudo aplicar: RequiereRevision)
    [HttpGet("pagos"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PaginaDto<PagoAdminDto>>> Pagos([FromQuery] FiltroPagosAdminRequest f)
        => Ok(await _admin.ListarPagosAsync(User.IdActual(), f.Estado, f.Pagina, f.Tamano));

    // ---- Solo SuperUsuario
    [HttpPatch("usuarios/{usuarioId:guid}/rol"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<UsuarioDto>> CambiarRol(Guid usuarioId, [FromBody] CambiarRolRequest r)
        => Ok(await _admin.CambiarRolAsync(User.IdActual(), usuarioId, r.Rol));

    /// <summary>Registra un reembolso ya hecho en el panel de Wompi (la API nunca envía dinero).</summary>
    [HttpPost("pagos/{referencia}/reembolsado"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<PagoAdminDto>> Reembolsado(string referencia, [FromBody] ReembolsoRequest r)
        => Ok(await _admin.MarcarReembolsadoAsync(User.IdActual(), referencia, r.Nota));
}
