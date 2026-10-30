using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Moderacion;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize(Policy = Politicas.Moderador)]
[Route("api/v1/admin")]
public sealed class AdministracionController : ControllerBase
{
    private readonly IAdministracionService _admin;
    private readonly IOferenteService _oferentes;
    private readonly IDatosPersonalesService _datosPersonales;
    private readonly IFundacionService _fundaciones;
    public AdministracionController(IAdministracionService admin, IOferenteService oferentes, IDatosPersonalesService datosPersonales,
        IFundacionService fundaciones)
    {
        _admin = admin; _oferentes = oferentes; _datosPersonales = datosPersonales; _fundaciones = fundaciones;
    }

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

    // ---- Fotos de perfil (las que el detector dejó en duda)
    [HttpGet("fotos"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<FotosEnRevisionDto>> Fotos([FromServices] IFotoPerfilService fotos)
        => Ok(await fotos.ListarEnRevisionAsync(User.IdActual()));

    [HttpPost("fotos/{usuarioId:guid}/aprobar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> AprobarFoto(Guid usuarioId, [FromServices] IFotoPerfilService fotos)
    {
        await fotos.AprobarAsync(User.IdActual(), usuarioId);
        return NoContent();
    }

    [HttpPost("fotos/{usuarioId:guid}/rechazar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> RechazarFoto(Guid usuarioId, [FromBody] MotivoRequest r, [FromServices] IFotoPerfilService fotos)
    {
        await fotos.RechazarAsync(User.IdActual(), usuarioId, r.Motivo);
        return NoContent();
    }

    // ---- Publicaciones de riesgo (revisión previa)
    /// <summary>Las que esperan revisión: primero las de más riesgo y luego las que llevan más tiempo (hasta 100).</summary>
    [HttpGet("moderacion/publicaciones"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PublicacionesEnRevisionDto>> PublicacionesEnRevision([FromServices] IModeracionPublicacionesService moderacion)
        => Ok(await moderacion.ListarAsync(User.IdActual()));

    [HttpGet("moderacion/cola"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<ColaModeracionDto>> ColaModeracion([FromServices] IModeracionPublicacionesService moderacion)
        => Ok(await moderacion.ContarAsync(User.IdActual()));

    /// <summary>Aprueba varias a la vez (hasta 50). Las que otro moderador ya resolvió se ignoran.</summary>
    [HttpPost("moderacion/publicaciones/aprobar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<int>> AprobarPublicaciones([FromBody] AprobarPublicacionesRequest r,
        [FromServices] IModeracionPublicacionesService moderacion)
        => Ok(await moderacion.AprobarAsync(User.IdActual(), r.Ids));

    /// <summary>El dueño recibe el motivo y puede corregirla: al editarla vuelve a la fila.</summary>
    [HttpPost("moderacion/publicaciones/{publicacionId:guid}/rechazar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> RechazarPublicacion(Guid publicacionId, [FromBody] MotivoRequest r,
        [FromServices] IModeracionPublicacionesService moderacion)
    {
        await moderacion.RechazarAsync(User.IdActual(), publicacionId, r.Motivo);
        return NoContent();
    }

    // ---- Negocios: el sello "Negocio verificado" (NIT revisado contra el RUES)
    [HttpGet("negocios"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<IReadOnlyList<NegocioPorVerificarDto>>> NegociosPorVerificar([FromServices] INegocioService negocios)
        => Ok(await negocios.ListarPorVerificarAsync(User.IdActual()));

    [HttpPost("negocios/{usuarioId:guid}/verificar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> VerificarNegocio(Guid usuarioId, [FromServices] INegocioService negocios)
    {
        await negocios.VerificarNitAsync(User.IdActual(), usuarioId);
        return NoContent();
    }

    /// <summary>El negocio recibe el motivo; vuelve a la fila cuando corrige el NIT o el nombre comercial.</summary>
    [HttpPost("negocios/{usuarioId:guid}/rechazar"), Authorize(Policy = Politicas.Moderador)]
    public async Task<IActionResult> RechazarNegocio(Guid usuarioId, [FromBody] MotivoRequest r, [FromServices] INegocioService negocios)
    {
        await negocios.RechazarNitAsync(User.IdActual(), usuarioId, r.Motivo);
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

    /// <summary>La cuenta declaró una fecha de menor de edad por error (comprobado por la moderación): puede registrarla de nuevo.</summary>
    [HttpPost("usuarios/{usuarioId:guid}/descartar-menor"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<UsuarioAdminDto>> DescartarMenor(Guid usuarioId, [FromBody] DescartarMenorRequest r)
        => Ok(await _admin.DescartarMenorDetectadoAsync(User.IdActual(), usuarioId, r.Motivo));

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
        => this.ArchivoCsv(await _admin.ExportarFacturasCsvAsync(User.IdActual(), estado), $"facturas-{estado.ToString().ToLowerInvariant()}.csv");

    // ---- PQR
    [HttpGet("pqr"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PaginaDto<PqrAdminDto>>> Pqr([FromQuery] FiltroPqrAdminRequest f)
        => Ok(await _admin.ListarPqrAsync(User.IdActual(), f.Estado, f.Pagina, f.Tamano));

    /// <summary>Quienes le vendieron al autor de la PQRS en compras completadas (sin sus datos de registro).</summary>
    [HttpGet("pqr/{id:guid}/vendedores"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<IReadOnlyList<VendedorDePqrDto>>> VendedoresDePqr(Guid id)
        => Ok(await _oferentes.ListarVendedoresDePqrAsync(User.IdActual(), id));

    /// <summary>
    /// Registro de oferente (Ley 1480, art. 53) de quien le vendió al autor de la PQRS, para resolver su reclamo. Exige una
    /// compra completada entre los dos y queda en la auditoría.
    /// </summary>
    [HttpGet("pqr/{id:guid}/vendedores/{usuarioId:guid}/oferente"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<DatosOferenteDto>> OferenteDePqr(Guid id, Guid usuarioId)
        => Ok(await _oferentes.ConsultarParaPqrAsync(User.IdActual(), id, usuarioId));

    [HttpPost("pqr/{id:guid}/responder"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PqrAdminDto>> ResponderPqr(Guid id, [FromBody] ResponderPqrRequest r)
        => Ok(await _admin.ResponderPqrAsync(User.IdActual(), id, r.Respuesta));

    /// <summary>Avisa a quien la radicó que su solicitud ya se está revisando.</summary>
    [HttpPost("pqr/{id:guid}/en-tramite"), Authorize(Policy = Politicas.Moderador)]
    public async Task<ActionResult<PqrAdminDto>> PqrEnTramite(Guid id)
        => Ok(await _admin.MarcarPqrEnTramiteAsync(User.IdActual(), id));

    // ---- Solo SuperUsuario: finanzas
    [HttpGet("ingresos"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<IngresosDto>> Ingresos([FromQuery] RangoFechasRequest r) => Ok(await _admin.ObtenerIngresosAsync(User.IdActual(), r));

    /// <summary>Pagos cobrados con su factura (conciliación con Wompi y contabilidad).</summary>
    [HttpGet("ingresos.csv"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> IngresosCsv([FromQuery] RangoFechasRequest r)
        => this.ArchivoCsv(await _admin.ExportarIngresosCsvAsync(User.IdActual(), r), "ingresos.csv");

    // ---- Solo SuperUsuario
    [HttpPatch("usuarios/{usuarioId:guid}/rol"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<UsuarioDto>> CambiarRol(Guid usuarioId, [FromBody] CambiarRolRequest r)
        => Ok(await _admin.CambiarRolAsync(User.IdActual(), usuarioId, r.Rol));

    /// <summary>
    /// Elimina (anonimiza) la cuenta de otra persona: retira sus publicaciones, cierra sus sesiones y borra sus fotos.
    /// Para cuentas de prueba o duplicadas. No aplica a uno mismo ni a otro SuperUsuario; queda en la auditoría con el motivo.
    /// </summary>
    [HttpPost("usuarios/{usuarioId:guid}/eliminar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> EliminarCuenta(Guid usuarioId, [FromBody] MotivoRequest r)
    {
        await _datosPersonales.EliminarCuentaPorAdministracionAsync(User.IdActual(), usuarioId, r.Motivo);
        return NoContent();
    }

    // ---- Solo SuperUsuario: factores de impacto
    /// <summary>Cambia el factor de CO₂ de un material con su fuente y una versión nueva de la metodología (queda en la auditoría).</summary>
    [HttpPut("impacto/factores/{material}"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<FactorImpactoDto>> ActualizarFactorImpacto(MaterialDto material, [FromBody] ActualizarFactorImpactoRequest r,
        [FromServices] IImpactoService impacto)
        => Ok(await impacto.ActualizarFactorAsync(User.IdActual(), material, r));

    // ---- Solo SuperUsuario: fundaciones verificadas
    /// <summary>Por estado (Pendiente por defecto). En las pendientes, el certificado llega como enlace que caduca en 5 minutos.</summary>
    [HttpGet("fundaciones"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<IReadOnlyList<FundacionAdminDto>>> Fundaciones([FromQuery] EstadoFundacionDto estado = EstadoFundacionDto.Pendiente)
        => Ok(await _fundaciones.ListarParaRevisionAsync(User.IdActual(), estado));

    [HttpPost("fundaciones/{usuarioId:guid}/aprobar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> AprobarFundacion(Guid usuarioId)
    {
        await _fundaciones.AprobarAsync(User.IdActual(), usuarioId);
        return NoContent();
    }

    [HttpPost("fundaciones/{usuarioId:guid}/rechazar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> RechazarFundacion(Guid usuarioId, [FromBody] MotivoRequest r)
    {
        await _fundaciones.RechazarAsync(User.IdActual(), usuarioId, r.Motivo);
        return NoContent();
    }

    /// <summary>Quita la insignia a una fundación verificada (p. ej. se disolvió o la usó para engañar).</summary>
    [HttpPost("fundaciones/{usuarioId:guid}/retirar"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> RetirarFundacion(Guid usuarioId, [FromBody] MotivoRequest r)
    {
        await _fundaciones.RetirarAsync(User.IdActual(), usuarioId, r.Motivo);
        return NoContent();
    }

    /// <summary>Marca o quita el Régimen Tributario Especial de una fundación verificada, con la evidencia revisada (queda en la auditoría).</summary>
    [HttpPost("fundaciones/{usuarioId:guid}/regimen"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<IActionResult> RegimenFundacion(Guid usuarioId, [FromBody] RegimenFundacionRequest r)
    {
        await _fundaciones.MarcarRegimenAsync(User.IdActual(), usuarioId, r);
        return NoContent();
    }

    /// <summary>Registra un reembolso ya hecho en el panel de Wompi (la API nunca envía dinero).</summary>
    [HttpPost("pagos/{referencia}/reembolsado"), Authorize(Policy = Politicas.SuperUsuario)]
    public async Task<ActionResult<PagoAdminDto>> Reembolsado(string referencia, [FromBody] ReembolsoRequest r)
        => Ok(await _admin.MarcarReembolsadoAsync(User.IdActual(), referencia, r.Nota));
}
