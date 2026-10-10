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

    // ---- Facturación electrónica: cola de facturas por emitir ante la DIAN
    [HttpGet("facturas"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PaginaDto<FacturaAdminDto>>> Facturas([FromQuery] FiltroFacturasAdminRequest f)
        => Ok(await _admin.ListarFacturasAsync(User.IdActual(), f.Estado, f.Pagina, f.Tamano));

    /// <summary>Registra número y CUFE de una factura ya emitida en el sistema de facturación (DIAN o proveedor).</summary>
    [HttpPost("facturas/{id:guid}/emitida"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<FacturaAdminDto>> FacturaEmitida(Guid id, [FromBody] EmitirFacturaRequest r)
        => Ok(await _admin.MarcarFacturaEmitidaAsync(User.IdActual(), id, r.NumeroDian, r.Cufe));

    /// <summary>
    /// SuperUsuario: la persona necesita la factura a su nombre (se equivocó al elegir o hubo un error). Si la factura está
    /// pendiente se corrige; si ya se emitió, queda "Reemplazada" (para nota crédito) y se crea otra pendiente. Devuelve la vigente.
    /// </summary>
    [HttpPut("facturas/{id:guid}/comprador"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<FacturaAdminDto>> CorregirCompradorFactura(Guid id, [FromBody] CorregirCompradorFacturaRequest r)
        => Ok(await _admin.CorregirCompradorFacturaAsync(User.IdActual(), id, r));

    /// <summary>CSV (separado por ";") con los datos del comprador, para cargar las facturas en el sistema de facturación.</summary>
    [HttpGet("facturas.csv"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> FacturasCsv([FromQuery] EstadoFacturaDto estado = EstadoFacturaDto.Pendiente)
        => ArchivoCsv(await _admin.ExportarFacturasCsvAsync(User.IdActual(), estado), $"facturas-{estado.ToString().ToLowerInvariant()}.csv");

    // ---- PQR
    [HttpGet("pqr"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PaginaDto<PqrAdminDto>>> Pqr([FromQuery] FiltroPqrAdminRequest f)
        => Ok(await _admin.ListarPqrAsync(User.IdActual(), f.Estado, f.Pagina, f.Tamano));

    [HttpPost("pqr/{id:guid}/responder"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PqrAdminDto>> ResponderPqr(Guid id, [FromBody] ResponderPqrRequest r)
        => Ok(await _admin.ResponderPqrAsync(User.IdActual(), id, r.Respuesta));

    // ---- Solo SuperUsuario: finanzas
    [HttpGet("ingresos"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<IngresosDto>> Ingresos([FromQuery] RangoFechasRequest r) => Ok(await _admin.ObtenerIngresosAsync(User.IdActual(), r));

    /// <summary>Pagos cobrados con su factura (conciliación con Wompi y contabilidad).</summary>
    [HttpGet("ingresos.csv"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> IngresosCsv([FromQuery] RangoFechasRequest r)
        => ArchivoCsv(await _admin.ExportarIngresosCsvAsync(User.IdActual(), r), "ingresos.csv");

    /// <summary>UTF-8 con BOM para que Excel en español muestre bien las tildes.</summary>
    private FileContentResult ArchivoCsv(string contenido, string nombre)
    {
        Response.Headers.CacheControl = "no-store";
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(contenido)).ToArray(),
            "text/csv; charset=utf-8", nombre);
    }

    // ---- Solo SuperUsuario
    [HttpPatch("usuarios/{usuarioId:guid}/rol"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<UsuarioDto>> CambiarRol(Guid usuarioId, [FromBody] CambiarRolRequest r)
        => Ok(await _admin.CambiarRolAsync(User.IdActual(), usuarioId, r.Rol));

    /// <summary>Registra un reembolso ya hecho en el panel de Wompi (la API nunca envía dinero).</summary>
    [HttpPost("pagos/{referencia}/reembolsado"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<PagoAdminDto>> Reembolsado(string referencia, [FromBody] ReembolsoRequest r)
        => Ok(await _admin.MarcarReembolsadoAsync(User.IdActual(), referencia, r.Nota));
}
