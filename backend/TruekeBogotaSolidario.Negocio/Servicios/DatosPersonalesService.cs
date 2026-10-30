using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Derechos del titular según la Ley 1581 de 2012: conocer (exportar) y suprimir sus datos.</summary>
public interface IDatosPersonalesService
{
    Task<DatosPersonalesDto> ExportarAsync(Guid actorId);
    Task EliminarCuentaAsync(Guid actorId, EliminarCuentaRequest r);
    /// <summary>El SuperUsuario elimina la cuenta de otra persona (p. ej. cuentas de prueba o duplicadas). Mismo efecto que la propia.</summary>
    Task EliminarCuentaPorAdministracionAsync(Guid superUsuarioId, Guid usuarioId, string motivo);
}

public sealed class DatosPersonalesService : IDatosPersonalesService
{
    private const int Maximo = 1000;

    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _pubs;
    private readonly ISolicitudRepository _solicitudes;
    private readonly IComentarioRepository _comentarios;
    private readonly IConversacionRepository _conversaciones;
    private readonly ITransaccionRepository _transacciones;
    private readonly IPagoRepository _pagos;
    private readonly INotificacionRepository _notificaciones;
    private readonly IDenunciaRepository _denuncias;
    private readonly ISesionRefreshRepository _refrescos;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IPublicacionService _publicacionService;
    private readonly ISolicitudService _solicitudService;
    private readonly ConfirmadorIdentidad _confirmador;
    private readonly IAlmacenArchivos _almacen;
    private readonly ISesionService _sesiones;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ICalificacionRepository _calificaciones;
    private readonly IFavoritoRepository _favoritos;
    private readonly IFacturaRepository _facturas;
    private readonly IPqrRepository _pqrs;
    private readonly IBloqueoService _bloqueos;
    private readonly ISuscripcionPushRepository _suscripcionesPush;
    private readonly IBusquedaGuardadaService _busquedas;
    private readonly IBusquedaGuardadaRepository _busquedasRepo;
    private readonly IMovimientoEcoPuntosRepository _movimientos;
    private readonly IDeseoService _deseos;
    private readonly IDeseoRepository _deseosRepo;
    private readonly IOferenteService _oferentes;
    private readonly IFundacionService _fundaciones;
    private readonly IEmpresaService _empresas;
    private readonly ILogger<DatosPersonalesService> _log;

    public DatosPersonalesService(IUsuarioRepository usuarios, IPublicacionRepository pubs, ISolicitudRepository solicitudes,
        IComentarioRepository comentarios, IConversacionRepository conversaciones, ITransaccionRepository transacciones, IPagoRepository pagos,
        INotificacionRepository notificaciones, IDenunciaRepository denuncias, ISesionRefreshRepository refrescos, IAuditoriaRepository auditoria,
        IUnidadDeTrabajo uow, IPublicacionService publicacionService, ISolicitudService solicitudService, ConfirmadorIdentidad confirmador,
        IAlmacenArchivos almacen, ISesionService sesiones, INotificador notificador, TimeProvider reloj,
        ICalificacionRepository calificaciones, IFavoritoRepository favoritos, IFacturaRepository facturas, IPqrRepository pqrs,
        IBloqueoService bloqueos, ILogger<DatosPersonalesService> log, ISuscripcionPushRepository suscripcionesPush,
        IBusquedaGuardadaService busquedas, IBusquedaGuardadaRepository busquedasRepo, IMovimientoEcoPuntosRepository movimientos,
        IDeseoService deseos, IDeseoRepository deseosRepo, IOferenteService oferentes, IFundacionService fundaciones,
        IEmpresaService empresas)
    {
        _oferentes = oferentes; _fundaciones = fundaciones; _empresas = empresas;
        _movimientos = movimientos; _deseos = deseos; _deseosRepo = deseosRepo;
        _bloqueos = bloqueos; _suscripcionesPush = suscripcionesPush; _busquedas = busquedas; _busquedasRepo = busquedasRepo;
        _calificaciones = calificaciones; _favoritos = favoritos; _facturas = facturas; _pqrs = pqrs;
        _usuarios = usuarios; _pubs = pubs; _solicitudes = solicitudes; _comentarios = comentarios; _conversaciones = conversaciones;
        _transacciones = transacciones; _pagos = pagos; _notificaciones = notificaciones; _denuncias = denuncias; _refrescos = refrescos;
        _auditoria = auditoria; _uow = uow; _publicacionService = publicacionService; _solicitudService = solicitudService; _confirmador = confirmador;
        _almacen = almacen; _sesiones = sesiones; _notificador = notificador; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<DatosPersonalesDto> ExportarAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var ahora = Ahora;

        var comentarios = (await _comentarios.ListarDelAutorAsync(actorId, Maximo))
            .Select(c => new ComentarioExportDto(c.Id, c.PublicacionId, c.Texto, c.FechaUtc, c.EstaOculto)).ToList();
        var mensajes = (await _conversaciones.ListarMensajesDelAutorAsync(actorId, Maximo))
            .Select(m => new MensajeExportDto(m.Id, m.ConversacionId, m.Texto, m.FechaUtc)).ToList();
        var transacciones = (await _transacciones.ListarPorUsuarioAsync(actorId)).Select(t =>
        {
            var soyOferente = t.OferenteId == actorId;
            return new TransaccionExportDto(t.Id, t.PublicacionId, t.Modo.ToString(), soyOferente ? "Oferente" : "Receptor",
                soyOferente ? t.PuntosOtorgadosOferente : t.PuntosOtorgadosReceptor, t.FechaUtc);
        }).ToList();
        var pagos = (await _pagos.ListarPorUsuarioAsync(actorId, Maximo))
            .Select(p => new PagoEstadoDto(p.Referencia, p.Concepto.ToString(), p.MontoCop, p.Estado.ToString(), p.FechaUtc, p.FechaResolucionUtc)).ToList();
        var notificaciones = (await _notificaciones.ListarTodasAsync(actorId, Maximo)).Select(NotificadorPersistente.ADto).ToList();
        var denuncias = (await _denuncias.ListarDelDenuncianteAsync(actorId, Maximo))
            .Select(d => new DenunciaExportDto(d.Id, d.Tipo.ToString(), d.ObjetivoId, d.Motivo.ToString(), d.Detalle, d.Estado.ToString(), d.FechaUtc)).ToList();

        var calificaciones = (await _calificaciones.ListarDelAutorAsync(actorId, Maximo))
            .Select(c => new CalificacionExportDto(c.Id, c.SolicitudId, c.Estrellas, c.Comentario, c.FechaUtc)).ToList();
        var favoritos = new List<Guid>();
        for (var pagina = 1; ; pagina++)
        {
            var (ids, total) = await _favoritos.ListarAsync(actorId, pagina, 50);
            favoritos.AddRange(ids);
            if (ids.Count == 0 || favoritos.Count >= total) break;
        }

        return new DatosPersonalesDto(ahora, Mapeos.AUsuarioDto(u, ahora), u.PoliticaDatosVersion, u.FechaAceptacionPolitica,
            await _publicacionService.ListarMiasAsync(actorId), await _solicitudService.ListarEnviadasAsync(actorId),
            comentarios, mensajes, transacciones, pagos, notificaciones, denuncias, calificaciones, favoritos,
            Mapeos.ADatosFacturacion(u), (await _facturas.ListarPorUsuarioAsync(actorId, 500)).Select(Mapeos.AFacturaDto).ToList(),
            (await _pqrs.ListarPorUsuarioAsync(actorId, 200)).Select(Mapeos.APqrDto).ToList(),
            (await _denuncias.ListarApelacionesDelUsuarioAsync(actorId, Maximo)).Select(DenunciaService.AApelacionDto).ToList(),
            await _bloqueos.PreferenciasAsync(actorId), await _bloqueos.ListarAsync(actorId), await _busquedas.ListarAsync(actorId),
            (await _movimientos.ListarPorUsuarioAsync(actorId, 1, Maximo)).Items
                .Select(m => new MovimientoEcoPuntosDto(m.Id, m.Concepto.ToString(), m.Puntos, m.Detalle, m.ReferenciaId, m.SaldoResultante, m.FechaUtc)).ToList(),
            await _deseos.ListarAsync(actorId), _oferentes.Descifrar(u), await _fundaciones.MiaAsync(actorId));
    }


    public async Task EliminarCuentaAsync(Guid actorId, EliminarCuentaRequest r)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        await _confirmador.ExigirAsync(u, r.Clave, r.GoogleIdToken); // con un token robado no basta para borrar la cuenta
        if (await _pagos.ContarPendientesAsync(actorId, DateTime.MinValue) > 0)
            throw new ReglaDeNegocioException("Tienes pagos en proceso. Espera a que se resuelvan (máximo una hora) y vuelve a intentarlo.");
        if (u.Rol == RolUsuarioEnum.SuperUsuario && await _usuarios.ContarPorRolAsync(RolUsuarioEnum.SuperUsuario) <= 1)
            throw new ReglaDeNegocioException("Eres el único SuperUsuario: asigna otro antes de eliminar tu cuenta.");

        await SuprimirAsync(u, actorId, "Cuenta eliminada por su titular.", "CUENTA_ELIMINADA", null);
        _log.LogWarning("Cuenta eliminada (anonimizada) por su titular {UsuarioId}", actorId);
    }

    public async Task EliminarCuentaPorAdministracionAsync(Guid superUsuarioId, Guid usuarioId, string motivo)
    {
        // El rol se recomprueba contra la base: un JWT emitido antes de quitarle el rol no basta.
        var actor = await _usuarios.ObtenerPorIdAsync(superUsuarioId);
        if (actor is null || actor.EstaEliminado || actor.Rol != RolUsuarioEnum.SuperUsuario)
            throw new AccesoDenegadoException("Solo el SuperUsuario puede eliminar cuentas.");
        if (usuarioId == superUsuarioId)
            throw new ReglaDeNegocioException("Para eliminar tu propia cuenta usa Mi cuenta → Privacidad y datos.");
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (u is null || u.EstaEliminado) throw new NoEncontradoException("La cuenta no existe o ya fue eliminada.");
        if (u.Rol == RolUsuarioEnum.SuperUsuario)
            throw new ReglaDeNegocioException("No se puede eliminar a otro SuperUsuario. Quítale el rol primero.");
        if (await _pagos.ContarPendientesAsync(usuarioId, DateTime.MinValue) > 0)
            throw new ReglaDeNegocioException("La cuenta tiene pagos en proceso. Espera a que se resuelvan (máximo una hora).");

        await SuprimirAsync(u, superUsuarioId, "Cuenta eliminada por la administración.", "CUENTA_ELIMINADA_ADMIN", motivo.Trim());
        _log.LogWarning("Cuenta {UsuarioId} eliminada (anonimizada) por el SuperUsuario {ActorId}", usuarioId, superUsuarioId);
    }

    /// <summary>Supresión (Ley 1581): retira el contenido, cierra sesiones, anonimiza y borra los archivos de <paramref name="u"/>.</summary>
    private async Task SuprimirAsync(Usuario u, Guid autorId, string razon, string accionAuditoria, string? detalle)
    {
        var actorId = u.Id;
        var ahora = Ahora;
        var avisos = new List<(Guid UsuarioId, string Tipo, string Mensaje, Guid Recurso)>();

        // 1) Solicitudes en curso: las aceptadas quedan "no concretadas"; las pendientes propias se cancelan y las recibidas se rechazan
        foreach (var s in await _solicitudes.ListarPendientesDelUsuarioAsync(actorId))
        {
            if (s.Estado == EstadoSolicitud.Aceptada)
            {
                s.MarcarNoConcretada("Una de las partes eliminó su cuenta.", ahora);
                s.Publicacion!.VolverADisponible();
                var otra = s.SolicitanteId == actorId ? s.Publicacion.PropietarioId : s.SolicitanteId;
                avisos.Add((otra, TiposNotificacion.IntercambioNoConcretado, $"El intercambio de \"{s.Publicacion.Titulo}\" se canceló: la otra persona eliminó su cuenta.", s.Id));
            }
            else if (s.SolicitanteId == actorId)
            {
                s.Cancelar(); // pendiente: la publicación no cambia
                avisos.Add((s.Publicacion!.PropietarioId, TiposNotificacion.SolicitudCancelada, $"Se canceló una solicitud para \"{s.Publicacion.Titulo}\".", s.Id));
            }
            else
            {
                s.Rechazar("La cuenta del propietario fue eliminada."); // sus publicaciones se retiran en el paso 2
                avisos.Add((s.SolicitanteId, TiposNotificacion.SolicitudRechazada, $"La publicación \"{s.Publicacion!.Titulo}\" ya no está disponible.", s.Id));
            }
        }
        // 2) Publicaciones activas: se retiran del catálogo
        foreach (var p in await _pubs.ListarActivasParaActualizarAsync(actorId)) p.Cancelar(razon);
        // 3) Comentarios públicos: se ocultan (los mensajes privados quedan para la contraparte, sin nombre del autor)
        foreach (var c in await _comentarios.ListarDelAutorAsync(actorId, 10_000)) if (!c.EstaOculto) c.Ocultar(razon);
        // 4) Sesiones y datos personales
        await _refrescos.RevocarTodasDelUsuarioAsync(actorId, ahora);
        foreach (var s in await _suscripcionesPush.DeUsuarioAsync(actorId)) _suscripcionesPush.Quitar(s); // ningún aviso más
        foreach (var b in await _busquedasRepo.DeUsuarioAsync(actorId)) _busquedasRepo.Quitar(b);
        foreach (var d in await _deseosRepo.DeUsuarioAsync(actorId)) _deseosRepo.Quitar(d); // dejan de verse en el perfil
        await _fundaciones.QuitarDeCuentaAsync(actorId); // perfil y necesidades; el certificado se borra con sus archivos
        await _empresas.QuitarDeCuentaAsync(actorId); // su membresía; la empresa y sus informes siguen
        u.Anonimizar(ahora);
        _auditoria.Agregar(new AuditoriaEvento(autorId, accionAuditoria, "Usuario", actorId, detalle, ahora));
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(actorId);

        // 5) Fuera de la transacción: archivos y avisos (si fallan, no deshacen la eliminación)
        try { await _almacen.EliminarDelUsuarioAsync(actorId); }
        catch (Exception ex) { _log.LogError(ex, "No se pudieron borrar los archivos del usuario eliminado {UsuarioId}", actorId); }
        foreach (var a in avisos) await _notificador.NotificarAsync(a.UsuarioId, a.Tipo, a.Mensaje, a.Recurso);
    }
}
