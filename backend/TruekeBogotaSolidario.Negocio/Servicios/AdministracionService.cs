using System.Text;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IAdministracionService
{
    // Moderación (Administrador y SuperUsuario)
    Task OcultarPublicacionAsync(Guid actorId, Guid publicacionId, string motivo);
    Task MostrarPublicacionAsync(Guid actorId, Guid publicacionId);
    Task<IReadOnlyList<VerificacionPendienteDto>> ListarVerificacionesPendientesAsync(Guid actorId);
    Task AprobarVerificacionAsync(Guid actorId, Guid usuarioId);
    Task RechazarVerificacionAsync(Guid actorId, Guid usuarioId, string motivo);
    Task<PaginaDto<UsuarioAdminDto>> BuscarUsuariosAsync(Guid actorId, BuscarUsuariosRequest r);
    /// <summary>Un Administrador suspende Clientes; solo un SuperUsuario suspende Administradores; a un SuperUsuario no se le suspende.</summary>
    Task<UsuarioAdminDto> SuspenderAsync(Guid actorId, Guid usuarioId, string motivo, int? dias);
    Task<UsuarioAdminDto> ReactivarAsync(Guid actorId, Guid usuarioId);
    Task<PaginaDto<PagoAdminDto>> ListarPagosAsync(Guid actorId, EstadoPagoDto estado, int pagina, int tamano);
    // Facturación y PQR (Administrador y SuperUsuario)
    Task<PaginaDto<FacturaAdminDto>> ListarFacturasAsync(Guid actorId, EstadoFacturaDto estado, int pagina, int tamano);
    /// <summary>Registra el número y el CUFE asignados por la DIAN (o el proveedor tecnológico) a una factura pendiente.</summary>
    Task<FacturaAdminDto> MarcarFacturaEmitidaAsync(Guid actorId, Guid facturaId, string numeroDian, string cufe);
    /// <summary>CSV con los datos del comprador para cargar las facturas en el sistema de facturación.</summary>
    Task<string> ExportarFacturasCsvAsync(Guid actorId, EstadoFacturaDto estado);
    Task<PaginaDto<PqrAdminDto>> ListarPqrAsync(Guid actorId, EstadoPqrDto estado, int pagina, int tamano);
    Task<PqrAdminDto> ResponderPqrAsync(Guid actorId, Guid pqrId, string respuesta);
    // Solo SuperUsuario
    Task<UsuarioDto> CambiarRolAsync(Guid actorId, Guid usuarioObjetivoId, RolDto nuevoRol);
    /// <summary>
    /// Registra que el dinero ya se devolvió por el panel de Wompi (la API nunca mueve dinero hacia afuera). Si el pago
    /// estaba aprobado, revierte el beneficio (plan, puntos, destacado o verificación pendiente) y marca la factura.
    /// </summary>
    Task<PagoAdminDto> MarcarReembolsadoAsync(Guid actorId, string referencia, string nota);
    Task<IngresosDto> ObtenerIngresosAsync(Guid actorId, RangoFechasRequest r);
    /// <summary>Pagos cobrados en el rango con su factura (para contabilidad y conciliación con Wompi).</summary>
    Task<string> ExportarIngresosCsvAsync(Guid actorId, RangoFechasRequest r);
}

/// <summary>
/// Defensa en profundidad: aunque Presentacion ya exige el rol en el endpoint, aquí se vuelve a comprobar
/// contra la base de datos (nunca contra lo que dice el token).
/// </summary>
public sealed class AdministracionService : IAdministracionService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _pubs;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAuditoriaRepository _auditoria;
    private readonly ISesionService _sesiones;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly ISesionRefreshRepository _refrescos;
    private readonly IAlmacenArchivos _almacen;
    private readonly IPagoRepository _pagos;
    private readonly ICorreoSaliente _correo;
    private readonly IFacturaRepository _facturas;
    private readonly IPqrRepository _pqrs;
    private readonly ILogger<AdministracionService> _log;

    public AdministracionService(IUsuarioRepository usuarios, IPublicacionRepository pubs, IUnidadDeTrabajo uow,
        IAuditoriaRepository auditoria, ISesionService sesiones, TimeProvider reloj, INotificador notificador,
        ISesionRefreshRepository refrescos, IAlmacenArchivos almacen, IPagoRepository pagos, ICorreoSaliente correo,
        IFacturaRepository facturas, IPqrRepository pqrs, ILogger<AdministracionService> log)
    {
        _usuarios = usuarios; _pubs = pubs; _uow = uow; _auditoria = auditoria; _sesiones = sesiones; _reloj = reloj;
        _notificador = notificador; _refrescos = refrescos; _almacen = almacen; _pagos = pagos; _correo = correo;
        _facturas = facturas; _pqrs = pqrs; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public static UsuarioAdminDto AUsuarioAdmin(Usuario u, DateTime ahora)
        => new(u.Id, u.NombreCompleto, u.Correo, u.Localidad, u.Rol.ToString(), u.FechaRegistro, u.CorreoVerificado,
            u.EstadoVerificacion.ToString(), u.SuspensionVigente(ahora), u.SuspensionVigente(ahora) ? u.SuspendidoHasta : null,
            u.SuspensionVigente(ahora) ? u.MotivoSuspension : null, u.DosFactoresActivo, u.Reputacion, u.CalificacionPromedio, u.CalificacionesTotal);

    private static PagoAdminDto APagoAdmin(Pago p)
        => new(p.Referencia, p.UsuarioId, p.Concepto.ToString(), p.MontoCop, p.PuntosCanjeados, p.Estado.ToString(), p.FechaUtc,
            p.FechaResolucionUtc, p.ProveedorTransaccionId, p.NotaInterna);

    public async Task<PaginaDto<UsuarioAdminDto>> BuscarUsuariosAsync(Guid actorId, BuscarUsuariosRequest r)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var (items, total) = await _usuarios.BuscarAsync(r.Texto, r.SoloSuspendidos, r.Pagina, r.Tamano);
        var ahora = Ahora;
        return new PaginaDto<UsuarioAdminDto>(items.Select(u => AUsuarioAdmin(u, ahora)).ToList(), total, Math.Max(r.Pagina, 1), Math.Clamp(r.Tamano, 1, 50));
    }

    private async Task<(Usuario Actor, Usuario Objetivo)> CargarParaModerarCuentaAsync(Guid actorId, Guid usuarioId)
    {
        var actor = await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes moderar tu propia cuenta.");
        var objetivo = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (objetivo is null || objetivo.EstaEliminado) throw new NoEncontradoException("Usuario no encontrado.");
        if (objetivo.Rol == RolUsuarioEnum.SuperUsuario) throw new AccesoDenegadoException("Un SuperUsuario no puede ser suspendido.");
        if (objetivo.Rol == RolUsuarioEnum.Administrador && actor.Rol != RolUsuarioEnum.SuperUsuario)
            throw new AccesoDenegadoException("Solo un SuperUsuario puede suspender a un Administrador.");
        return (actor, objetivo);
    }

    public async Task<UsuarioAdminDto> SuspenderAsync(Guid actorId, Guid usuarioId, string motivo, int? dias)
    {
        var (_, u) = await CargarParaModerarCuentaAsync(actorId, usuarioId);
        var ahora = Ahora;
        u.Suspender(motivo, dias.HasValue ? ahora.AddDays(dias.Value) : null, ahora); // sube VersionSeguridad: tokens de acceso revocados
        await _refrescos.RevocarTodasDelUsuarioAsync(u.Id, ahora);
        Auditar(actorId, "USUARIO_SUSPENDIDO", "Usuario", u.Id, $"{(dias.HasValue ? $"{dias} días" : "indefinida")}: {motivo}");
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
        _log.LogWarning("Usuario {UsuarioId} suspendido por {ActorId}", u.Id, actorId);

        // No puede iniciar sesión: se le avisa por correo (el motivo es suyo, no de terceros)
        var hasta = u.SuspendidoHasta is { } h ? $"hasta el {h:yyyy-MM-dd} (UTC)" : "hasta nuevo aviso";
        _correo.Encolar(PlantillaCorreo.Crear(u.Correo, "Tu cuenta de Trueke Bogotá Solidario fue suspendida",
            $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
            new[] { $"Tu cuenta fue suspendida {hasta}.", $"Motivo: {u.MotivoSuspension}" },
            pie: "Si crees que es un error, responde a este correo y lo revisaremos."));
        return AUsuarioAdmin(u, ahora);
    }

    public async Task<UsuarioAdminDto> ReactivarAsync(Guid actorId, Guid usuarioId)
    {
        var (_, u) = await CargarParaModerarCuentaAsync(actorId, usuarioId);
        u.Reactivar();
        Auditar(actorId, "USUARIO_REACTIVADO", "Usuario", u.Id, null);
        await _uow.GuardarCambiosAsync();
        _correo.Encolar(PlantillaCorreo.Crear(u.Correo, "Tu cuenta de Trueke Bogotá Solidario fue reactivada",
            $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
            new[] { "Tu cuenta está activa de nuevo. Ya puedes iniciar sesión y seguir participando en la comunidad." }));
        return AUsuarioAdmin(u, Ahora);
    }

    public async Task<PaginaDto<PagoAdminDto>> ListarPagosAsync(Guid actorId, EstadoPagoDto estado, int pagina, int tamano)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var (items, total) = await _pagos.ListarPorEstadoAsync((EstadoPago)(int)estado, pagina, tamano);
        return new PaginaDto<PagoAdminDto>(items.Select(APagoAdmin).ToList(), total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    public async Task<PagoAdminDto> MarcarReembolsadoAsync(Guid actorId, string referencia, string nota)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        var p = await _pagos.ObtenerPorReferenciaAsync(referencia) ?? throw new NoEncontradoException("Pago no encontrado.");
        var ahora = Ahora;
        var estabaAprobado = p.Estado == EstadoPago.Aprobado;
        p.MarcarReembolsado(nota.Trim());
        if (estabaAprobado) await RevertirBeneficioAsync(p, ahora);
        (await _facturas.ObtenerPorPagoAsync(p.Id))?.RegistrarReembolso($"Reembolsado: {nota.Trim()}");
        Auditar(actorId, "PAGO_REEMBOLSADO", "Pago", p.Id, nota);
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(p.UsuarioId, TiposNotificacion.PagoReembolsado, $"Te reembolsamos {p.MontoCop:N0} COP ({p.Concepto}).", null);
        return APagoAdmin(p);
    }

    /// <summary>Retira lo que el pago dio: un reembolso no puede dejar el beneficio activo.</summary>
    private async Task RevertirBeneficioAsync(Pago p, DateTime ahora)
    {
        var u = await _usuarios.ObtenerPorIdAsync(p.UsuarioId);
        if (u is null) return;
        switch (p.Concepto)
        {
            case ConceptoPago.Premium: u.RevertirMesDePlan(TipoCuenta.Premium, ahora); break;
            case ConceptoPago.Empresa: u.RevertirMesDePlan(TipoCuenta.Empresa, ahora); break;
            case ConceptoPago.Recarga:
                var retirados = u.RetirarEcoPuntosHasta(p.MontoCop / PoliticaEcoPuntos.CopPorEcoPunto);
                _log.LogInformation("Reembolso {Referencia}: {Puntos} Eco-Puntos retirados", p.Referencia, retirados);
                break;
            case ConceptoPago.Destacar:
                if (p.PublicacionId is { } pubId && await _pubs.ObtenerPorIdAsync(pubId) is { } pub) pub.QuitarDestacado(ahora);
                break;
            case ConceptoPago.Verificar:
                u.CancelarVerificacionPendiente(); // si ya se aprobó, el servicio se prestó y la verificación se conserva
                break;
        }
    }

    // ---------------- Facturación ----------------
    public async Task<PaginaDto<FacturaAdminDto>> ListarFacturasAsync(Guid actorId, EstadoFacturaDto estado, int pagina, int tamano)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var (items, total) = await _facturas.ListarPorEstadoAsync((EstadoFactura)(int)estado, pagina, tamano);
        return new PaginaDto<FacturaAdminDto>(items.Select(Mapeos.AFacturaAdminDto).ToList(), total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 100));
    }

    public async Task<FacturaAdminDto> MarcarFacturaEmitidaAsync(Guid actorId, Guid facturaId, string numeroDian, string cufe)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var f = await _facturas.ObtenerAsync(facturaId) ?? throw new NoEncontradoException("Factura no encontrada.");
        f.MarcarEmitida(numeroDian, cufe, Ahora);
        Auditar(actorId, "FACTURA_EMITIDA", "Factura", f.Id, f.NumeroDian);
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(f.UsuarioId, TiposNotificacion.FacturaEmitida,
            $"Tu factura electrónica {f.NumeroDian} por {f.TotalCop:N0} COP ya fue emitida. Llegará a {f.CompradorCorreo}.", null);
        return Mapeos.AFacturaAdminDto(f);
    }

    public async Task<string> ExportarFacturasCsvAsync(Guid actorId, EstadoFacturaDto estado)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var (items, _) = await _facturas.ListarPorEstadoAsync((EstadoFactura)(int)estado, 1, 100);
        var sb = new StringBuilder();
        Csv.Fila(sb, "Id", "Referencia", "FechaUtc", "Concepto", "Descripcion", "TotalCop", "BaseCop", "IvaCop", "IvaPorcentaje",
            "TipoDocumento", "Documento", "Nombre", "Correo", "Direccion", "Municipio", "Estado", "NumeroDian", "Cufe", "RequiereNotaCredito");
        foreach (var f in items)
            Csv.Fila(sb, f.Id, f.Referencia, f.FechaUtc, f.Concepto, f.Descripcion, f.TotalCop, f.BaseCop, f.IvaCop, f.IvaPorcentaje,
                f.CompradorTipoDocumento?.ToString() ?? "ConsumidorFinal", f.CompradorDocumento, f.CompradorNombre, f.CompradorCorreo,
                f.CompradorDireccion, f.CompradorMunicipioCodigo is null ? null : Divipola.NombreCompleto(f.CompradorMunicipioCodigo),
                f.Estado, f.NumeroDian, f.Cufe, f.RequiereNotaCredito ? "Sí" : "No");
        Auditar(actorId, "FACTURAS_EXPORTADAS", "Factura", Guid.Empty, $"{estado}: {items.Count}");
        await _uow.GuardarCambiosAsync();
        return sb.ToString();
    }

    // ---------------- PQR ----------------
    private static PqrAdminDto APqrAdmin(Pqr p, Usuario? u, DateTime ahora)
        => new(p.Id, p.Radicado, p.Tipo.ToString(), p.Asunto, p.Descripcion, p.PagoReferencia, p.Estado.ToString(), p.FechaUtc,
            p.FechaLimiteUtc, p.Estado == EstadoPqr.Abierta && p.FechaLimiteUtc < ahora, p.Respuesta, p.FechaRespuestaUtc,
            p.UsuarioId, u?.NombreCompleto ?? "Usuario eliminado", u?.Correo ?? "");

    public async Task<PaginaDto<PqrAdminDto>> ListarPqrAsync(Guid actorId, EstadoPqrDto estado, int pagina, int tamano)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var (items, total) = await _pqrs.ListarPorEstadoAsync((EstadoPqr)(int)estado, pagina, tamano);
        var ahora = Ahora;
        var resultado = new List<PqrAdminDto>(items.Count);
        foreach (var p in items) resultado.Add(APqrAdmin(p, await _usuarios.ObtenerPorIdAsync(p.UsuarioId), ahora));
        return new PaginaDto<PqrAdminDto>(resultado, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    public async Task<PqrAdminDto> ResponderPqrAsync(Guid actorId, Guid pqrId, string respuesta)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var p = await _pqrs.ObtenerAsync(pqrId) ?? throw new NoEncontradoException("Solicitud no encontrada.");
        var ahora = Ahora;
        p.Responder(actorId, respuesta, ahora);
        Auditar(actorId, "PQR_RESPONDIDA", "Pqr", p.Id, p.Radicado);
        await _uow.GuardarCambiosAsync();
        var u = await _usuarios.ObtenerPorIdAsync(p.UsuarioId);
        if (u is not null && !u.EstaEliminado)
        {
            await NotificarAsync(u.Id, TiposNotificacion.PqrRespondida, $"Respondimos tu solicitud {p.Radicado}.", p.Id);
            _correo.Encolar(PlantillaCorreo.Crear(u.Correo, $"Respuesta a tu solicitud {p.Radicado}",
                $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                new[] { $"Asunto: {p.Asunto}", p.Respuesta! }));
        }
        return APqrAdmin(p, u, ahora);
    }

    // ---------------- Ingresos ----------------
    private (DateTime Desde, DateTime Hasta) Rango(RangoFechasRequest r)
    {
        var hasta = (r.Hasta ?? Ahora).ToUniversalTime();
        var desde = (r.Desde ?? hasta.AddMonths(-12)).ToUniversalTime();
        if (desde >= hasta) throw new ReglaDeNegocioException("La fecha inicial debe ser anterior a la final.");
        if (hasta - desde > TimeSpan.FromDays(3 * 366)) throw new ReglaDeNegocioException("El rango máximo es de 3 años.");
        return (desde, hasta);
    }

    public async Task<IngresosDto> ObtenerIngresosAsync(Guid actorId, RangoFechasRequest r)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        var (desde, hasta) = Rango(r);
        var resumen = await _pagos.ResumirIngresosAsync(desde, hasta);
        var (premium, empresa) = await _usuarios.ContarPlanesVigentesAsync(Ahora);
        var (_, facturasPendientes) = await _facturas.ListarPorEstadoAsync(EstadoFactura.Pendiente, 1, 1);
        var (_, pqrAbiertas) = await _pqrs.ListarPorEstadoAsync(EstadoPqr.Abierta, 1, 1);

        long Suma(IEnumerable<ResumenIngresos> xs) => xs.Sum(x => x.TotalCop);
        var cobrado = Suma(resumen); // aprobados + luego reembolsados (todo lo que entró)
        var reembolsado = Suma(resumen.Where(x => x.Estado == EstadoPago.Reembolsado));
        var porMes = resumen.GroupBy(x => (x.Anio, x.Mes)).OrderBy(g => g.Key)
            .Select(g => new IngresoMesDto(g.Key.Anio, g.Key.Mes, Suma(g), Suma(g.Where(x => x.Estado == EstadoPago.Reembolsado)), g.Sum(x => x.Cantidad)))
            .ToList();
        var porConcepto = resumen.GroupBy(x => x.Concepto).OrderByDescending(g => Suma(g))
            .Select(g => new IngresoConceptoDto(g.Key.ToString(), Suma(g), Suma(g.Where(x => x.Estado == EstadoPago.Reembolsado)), g.Sum(x => x.Cantidad)))
            .ToList();
        var recurrente = (long)premium * PoliticaEcoPuntos.PrecioPremiumCop + (long)empresa * PoliticaEcoPuntos.PrecioEmpresaCop;
        return new IngresosDto(desde, hasta, cobrado, reembolsado, cobrado - reembolsado, resumen.Sum(x => x.Cantidad), premium, empresa,
            recurrente, facturasPendientes, pqrAbiertas, porMes, porConcepto);
    }

    public async Task<string> ExportarIngresosCsvAsync(Guid actorId, RangoFechasRequest r)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        var (desde, hasta) = Rango(r);
        var pagos = await _pagos.ListarCobradosAsync(desde, hasta, 50_000);
        var facturas = (await _facturas.ListarPorPagosAsync(pagos.Select(p => p.Id).ToList())).ToDictionary(f => f.PagoId);
        var sb = new StringBuilder();
        Csv.Fila(sb, "Referencia", "FechaResolucionUtc", "Concepto", "Estado", "MontoCop", "PuntosCanjeados", "TransaccionWompi", "UsuarioId",
            "FacturaEstado", "NumeroDian", "Cufe", "BaseCop", "IvaCop");
        foreach (var p in pagos)
        {
            facturas.TryGetValue(p.Id, out var f);
            Csv.Fila(sb, p.Referencia, p.FechaResolucionUtc, p.Concepto, p.Estado, p.MontoCop, p.PuntosCanjeados, p.ProveedorTransaccionId, p.UsuarioId,
                f?.Estado, f?.NumeroDian, f?.Cufe, f?.BaseCop, f?.IvaCop);
        }
        Auditar(actorId, "INGRESOS_EXPORTADOS", "Pago", Guid.Empty, $"{desde:yyyy-MM-dd}..{hasta:yyyy-MM-dd}: {pagos.Count}");
        await _uow.GuardarCambiosAsync();
        return sb.ToString();
    }

    private Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid? recursoId)
        => _notificador.NotificarAsync(usuarioId, tipo, mensaje, recursoId);

    private async Task<Usuario> ExigirRolAsync(Guid actorId, RolUsuarioEnum minimo)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < minimo) throw new AccesoDenegadoException();
        return actor;
    }

    private void Auditar(Guid actorId, string accion, string tipo, Guid objetivoId, string? detalle)
        => _auditoria.Agregar(new AuditoriaEvento(actorId, accion, tipo, objetivoId, detalle, _reloj.GetUtcNow().UtcDateTime));

    public async Task OcultarPublicacionAsync(Guid actorId, Guid publicacionId, string motivo)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var p = await _pubs.ObtenerPorIdAsync(publicacionId) ?? throw new NoEncontradoException("Publicación no encontrada.");
        p.Ocultar(motivo);
        Auditar(actorId, "PUBLICACION_OCULTADA", "Publicacion", publicacionId, motivo);
        await _uow.GuardarCambiosAsync();
        _log.LogWarning("Publicación {PublicacionId} ocultada por moderador {ActorId}", publicacionId, actorId);
        await NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionOcultada, $"Tu publicación \"{p.Titulo}\" fue ocultada por moderación.", p.Id);
    }

    public async Task MostrarPublicacionAsync(Guid actorId, Guid publicacionId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var p = await _pubs.ObtenerPorIdAsync(publicacionId) ?? throw new NoEncontradoException("Publicación no encontrada.");
        p.Mostrar();
        Auditar(actorId, "PUBLICACION_RESTAURADA", "Publicacion", publicacionId, null);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Publicación {PublicacionId} restaurada por moderador {ActorId}", publicacionId, actorId);
        await NotificarAsync(p.PropietarioId, TiposNotificacion.PublicacionRestaurada, $"Tu publicación \"{p.Titulo}\" volvió a ser visible.", p.Id);
    }

    public async Task<IReadOnlyList<VerificacionPendienteDto>> ListarVerificacionesPendientesAsync(Guid actorId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        var lista = await _usuarios.ObtenerPorVerificacionAsync(EstadoVerificacion.Pendiente);
        var resultado = new List<VerificacionPendienteDto>(lista.Count);
        foreach (var u in lista)
        {
            // El documento de identidad vive en un contenedor privado: el moderador recibe un enlace que caduca en 5 minutos
            var enlace = u.DocumentoVerificacionUrl is null ? null : await _almacen.UrlLecturaTemporalAsync(u.DocumentoVerificacionUrl);
            resultado.Add(new VerificacionPendienteDto(u.Id, u.NombreCompleto, u.Correo, enlace ?? "", u.FechaRegistro));
        }
        return resultado;
    }

    public async Task AprobarVerificacionAsync(Guid actorId, Guid usuarioId)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes resolver tu propia verificación.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var documento = u.DocumentoVerificacionUrl;
        u.AprobarVerificacion();
        Auditar(actorId, "VERIFICACION_APROBADA", "Usuario", usuarioId, null);
        await _uow.GuardarCambiosAsync();
        if (documento is not null) await _almacen.EliminarDocumentoAsync(documento);
        _log.LogInformation("Verificación aprobada {UsuarioId} por {ActorId}", usuarioId, actorId);
        await NotificarAsync(usuarioId, TiposNotificacion.VerificacionAprobada, "Tu cuenta fue verificada.", null);
    }

    public async Task RechazarVerificacionAsync(Guid actorId, Guid usuarioId, string motivo)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.Administrador);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("No puedes resolver tu propia verificación.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var documento = u.DocumentoVerificacionUrl;
        u.RechazarVerificacion(motivo);
        Auditar(actorId, "VERIFICACION_RECHAZADA", "Usuario", usuarioId, motivo);
        await _uow.GuardarCambiosAsync();
        if (documento is not null) await _almacen.EliminarDocumentoAsync(documento);
        _log.LogInformation("Verificación rechazada {UsuarioId} por {ActorId}", usuarioId, actorId);
        await NotificarAsync(usuarioId, TiposNotificacion.VerificacionRechazada, "Tu solicitud de verificación fue rechazada. Revisa el motivo en tu perfil.", null);
    }

    public async Task<UsuarioDto> CambiarRolAsync(Guid actorId, Guid usuarioObjetivoId, RolDto nuevoRol)
    {
        await ExigirRolAsync(actorId, RolUsuarioEnum.SuperUsuario);
        if (actorId == usuarioObjetivoId) throw new ReglaDeNegocioException("No puedes cambiar tu propio rol.");
        var objetivo = await _usuarios.ObtenerPorIdAsync(usuarioObjetivoId) ?? throw new NoEncontradoException("Usuario no encontrado.");

        var rolAnterior = objetivo.Rol;
        objetivo.CambiarRol((RolUsuarioEnum)(int)nuevoRol); // sube VersionSeguridad: sus tokens anteriores quedan revocados
        if (rolAnterior == RolUsuarioEnum.SuperUsuario && objetivo.Rol != RolUsuarioEnum.SuperUsuario
            && await _usuarios.ContarPorRolAsync(RolUsuarioEnum.SuperUsuario) <= 1)
            throw new ReglaDeNegocioException("Debe existir al menos un SuperUsuario.");

        Auditar(actorId, "ROL_CAMBIADO", "Usuario", objetivo.Id, $"{rolAnterior}->{objetivo.Rol}");
        await _refrescos.RevocarTodasDelUsuarioAsync(objetivo.Id, _reloj.GetUtcNow().UtcDateTime); // debe volver a iniciar sesión
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(objetivo.Id);
        _log.LogWarning("Rol de {UsuarioId} cambiado {Anterior}→{Nuevo} por {ActorId}", objetivo.Id, rolAnterior, objetivo.Rol, actorId);
        return Mapeos.AUsuarioDto(objetivo, _reloj.GetUtcNow().UtcDateTime);
    }
}
