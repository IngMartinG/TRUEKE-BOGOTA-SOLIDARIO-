using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// La empresa de la propia cuenta: registro, miembros e invitaciones, campañas e informes. Nada es público: los datos de una
/// empresa solo los ven sus miembros. La verificación y la emisión de informes están en /admin/empresas.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/empresas")]
public sealed class EmpresasController : ControllerBase
{
    private readonly IEmpresaService _empresas;
    public EmpresasController(IEmpresaService empresas) => _empresas = empresas;

    /// <summary>La empresa a la que pertenece la cuenta (204 si no pertenece a ninguna).</summary>
    [HttpGet("mia")]
    public async Task<ActionResult<MiEmpresaDto>> Mia()
        => await _empresas.MiaAsync(User.IdActual()) is { } e ? Ok(e) : NoContent();

    /// <summary>Registra la empresa (o la vuelve a enviar tras un rechazo) con su NIT y el certificado de la Cámara de Comercio.</summary>
    [HttpPost("mia"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<MiEmpresaDto>> Solicitar([FromBody] SolicitarEmpresaRequest r, CancellationToken ct)
        => Ok(await _empresas.SolicitarAsync(User.IdActual(), r, ct));

    /// <summary>Solo Admin: crea un enlace de invitación (válido 7 días, un solo uso). El código no se puede volver a consultar.</summary>
    [HttpPost("mia/invitaciones"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<InvitacionCreadaDto>> Invitar([FromBody] CrearInvitacionEmpresaRequest r)
        => Ok(await _empresas.InvitarAsync(User.IdActual(), r));

    [HttpDelete("mia/invitaciones/{id:guid}")]
    public async Task<IActionResult> RevocarInvitacion(Guid id)
    {
        await _empresas.RevocarInvitacionAsync(User.IdActual(), id);
        return NoContent();
    }

    /// <summary>Acepta una invitación con el código del enlace.</summary>
    [HttpPost("unirse"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<MiEmpresaDto>> Unirse([FromBody] UnirseEmpresaRequest r)
        => Ok(await _empresas.UnirseAsync(User.IdActual(), r));

    [HttpPut("mia/miembros/{id:guid}/rol")]
    public async Task<ActionResult<MiEmpresaDto>> CambiarRol(Guid id, [FromBody] CambiarRolMiembroRequest r)
        => Ok(await _empresas.CambiarRolAsync(User.IdActual(), id, r.Rol));

    [HttpDelete("mia/miembros/{id:guid}")]
    public async Task<IActionResult> QuitarMiembro(Guid id)
    {
        await _empresas.QuitarMiembroAsync(User.IdActual(), id);
        return NoContent();
    }

    /// <summary>La cuenta sale de la empresa (el último Admin debe nombrar a otro antes).</summary>
    [HttpDelete("mia/membresia")]
    public async Task<IActionResult> Salir()
    {
        await _empresas.SalirAsync(User.IdActual());
        return NoContent();
    }

    [HttpPost("mia/campanas"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<CampanaDto>> CrearCampana([FromBody] GuardarCampanaRequest r)
        => Ok(await _empresas.CrearCampanaAsync(User.IdActual(), r));

    [HttpPut("mia/campanas/{id:guid}"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<CampanaDto>> EditarCampana(Guid id, [FromBody] GuardarCampanaRequest r)
        => Ok(await _empresas.EditarCampanaAsync(User.IdActual(), id, r));

    /// <summary>Para el selector al publicar: las campañas activas hoy (lista vacía si la cuenta no pertenece a una empresa verificada).</summary>
    [HttpGet("mia/campanas-activas")]
    public async Task<ActionResult<IReadOnlyList<CampanaDto>>> CampanasActivas() => Ok(await _empresas.CampanasActivasAsync(User.IdActual()));

    [HttpGet("mia/informes/{id:guid}/pdf")]
    public async Task<IActionResult> InformePdf(Guid id)
    {
        var a = await _empresas.InformePdfAsync(User.IdActual(), id);
        return this.ArchivoPdf(a.Contenido, a.Nombre);
    }

    /// <summary>Las mismas filas de la trazabilidad del PDF, separadas por ";".</summary>
    [HttpGet("mia/informes/{id:guid}/csv")]
    public async Task<IActionResult> InformeCsv(Guid id)
    {
        var a = await _empresas.InformeCsvAsync(User.IdActual(), id);
        return this.ArchivoCsv(a.Contenido, a.Nombre);
    }
}

/// <summary>
/// Administración de empresas: los Administradores consultan y emiten informes de impacto; verificar, rechazar y retirar
/// empresas es del SuperUsuario (con el certificado de la Cámara de Comercio).
/// </summary>
[ApiController]
[Authorize(Policy = Politicas.Moderador)]
[Route("api/v1/admin/empresas")]
public sealed class AdministracionEmpresasController : ControllerBase
{
    private readonly IEmpresaService _empresas;
    public AdministracionEmpresasController(IEmpresaService empresas) => _empresas = empresas;

    /// <summary>Por estado (todas si se omite), las más antiguas primero. El certificado y el correo solo los ve el SuperUsuario.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmpresaAdminDto>>> Listar([FromQuery] EstadoEmpresaDto? estado)
        => Ok(await _empresas.ListarAdminAsync(User.IdActual(), estado));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmpresaAdminDetalleDto>> Detalle(Guid id) => Ok(await _empresas.DetalleAdminAsync(User.IdActual(), id));

    [HttpPost("{id:guid}/aprobar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> Aprobar(Guid id)
    {
        await _empresas.AprobarAsync(User.IdActual(), id);
        return NoContent();
    }

    [HttpPost("{id:guid}/rechazar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> Rechazar(Guid id, [FromBody] MotivoRequest r)
    {
        await _empresas.RechazarAsync(User.IdActual(), id, r.Motivo);
        return NoContent();
    }

    [HttpPost("{id:guid}/retirar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> Retirar(Guid id, [FromBody] MotivoRequest r)
    {
        await _empresas.RetirarAsync(User.IdActual(), id, r.Motivo);
        return NoContent();
    }

    /// <summary>Emite un informe con una foto inmutable de los registros del periodo (queda en la auditoría y se avisa a los miembros).</summary>
    [HttpPost("{id:guid}/informes")]
    public async Task<ActionResult<InformeImpactoDto>> EmitirInforme(Guid id, [FromBody] EmitirInformeRequest r)
        => Ok(await _empresas.EmitirInformeAsync(User.IdActual(), id, r));

    [HttpGet("informes/{informeId:guid}/pdf")]
    public async Task<IActionResult> InformePdf(Guid informeId)
    {
        var a = await _empresas.InformePdfAdminAsync(User.IdActual(), informeId);
        return this.ArchivoPdf(a.Contenido, a.Nombre);
    }

    [HttpGet("informes/{informeId:guid}/csv")]
    public async Task<IActionResult> InformeCsv(Guid informeId)
    {
        var a = await _empresas.InformeCsvAdminAsync(User.IdActual(), informeId);
        return this.ArchivoCsv(a.Contenido, a.Nombre);
    }

    /// <summary>Fotos de la entrega de una solicitud (URLs firmadas de corta vida; queda en la auditoría).</summary>
    [HttpGet("~/api/v1/admin/solicitudes/{solicitudId:guid}/evidencias")]
    public async Task<ActionResult<IReadOnlyList<EvidenciaEntregaDto>>> Evidencias(Guid solicitudId, [FromServices] ISolicitudService solicitudes)
        => Ok(await solicitudes.EvidenciasAdminAsync(User.IdActual(), solicitudId));
}
