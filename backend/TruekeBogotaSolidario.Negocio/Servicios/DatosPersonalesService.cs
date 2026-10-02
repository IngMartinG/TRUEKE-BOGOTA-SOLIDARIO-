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
    private readonly IValidadorGoogle _google;
    private readonly IAlmacenArchivos _almacen;
    private readonly ISesionService _sesiones;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DatosPersonalesService> _log;

    public DatosPersonalesService(IUsuarioRepository usuarios, IPublicacionRepository pubs, ISolicitudRepository solicitudes,
        IComentarioRepository comentarios, IConversacionRepository conversaciones, ITransaccionRepository transacciones, IPagoRepository pagos,
        INotificacionRepository notificaciones, IDenunciaRepository denuncias, ISesionRefreshRepository refrescos, IAuditoriaRepository auditoria,
        IUnidadDeTrabajo uow, IPublicacionService publicacionService, ISolicitudService solicitudService, IValidadorGoogle google,
        IAlmacenArchivos almacen, ISesionService sesiones, INotificador notificador, TimeProvider reloj, ILogger<DatosPersonalesService> log)
    {
        _usuarios = usuarios; _pubs = pubs; _solicitudes = solicitudes; _comentarios = comentarios; _conversaciones = conversaciones;
        _transacciones = transacciones; _pagos = pagos; _notificaciones = notificaciones; _denuncias = denuncias; _refrescos = refrescos;
        _auditoria = auditoria; _uow = uow; _publicacionService = publicacionService; _solicitudService = solicitudService; _google = google;
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

        return new DatosPersonalesDto(ahora, Mapeos.AUsuarioDto(u, ahora), u.PoliticaDatosVersion, u.FechaAceptacionPolitica,
            await _publicacionService.ListarMiasAsync(actorId), await _solicitudService.ListarEnviadasAsync(actorId),
            comentarios, mensajes, transacciones, pagos, notificaciones, denuncias);
    }

    /// <summary>Volver a demostrar identidad: con un token robado (o un equipo desatendido) no basta para borrar la cuenta.</summary>
    private async Task ExigirReautenticacionAsync(Usuario u, EliminarCuentaRequest r)
    {
        if (u.TieneClave)
        {
            if (string.IsNullOrEmpty(r.Clave) || !u.VerificarClave(r.Clave))
                throw new ReglaDeNegocioException("La contraseña no es correcta.");
            return;
        }
        if (u.GoogleSub is not null && !string.IsNullOrEmpty(r.GoogleIdToken))
        {
            var id = await _google.ValidarAsync(r.GoogleIdToken);
            if (id is not null && id.Sub == u.GoogleSub) return;
        }
        throw new ReglaDeNegocioException("Confirma tu identidad con tu contraseña o volviendo a iniciar sesión con Google.");
    }

    public async Task EliminarCuentaAsync(Guid actorId, EliminarCuentaRequest r)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        await ExigirReautenticacionAsync(u, r);
        if (await _pagos.ContarPendientesAsync(actorId, DateTime.MinValue) > 0)
            throw new ReglaDeNegocioException("Tienes pagos en proceso. Espera a que se resuelvan (máximo una hora) y vuelve a intentarlo.");
        if (u.Rol == RolUsuarioEnum.SuperUsuario && await _usuarios.ContarPorRolAsync(RolUsuarioEnum.SuperUsuario) <= 1)
            throw new ReglaDeNegocioException("Eres el único SuperUsuario: asigna otro antes de eliminar tu cuenta.");

        var ahora = Ahora;
        var avisos = new List<(Guid UsuarioId, string Tipo, string Mensaje, Guid Recurso)>();

        // 1) Solicitudes en curso: las propias se cancelan; las recibidas se rechazan (y su publicación queda libre)
        foreach (var s in await _solicitudes.ListarPendientesDelUsuarioAsync(actorId))
        {
            if (s.SolicitanteId == actorId)
            {
                s.Cancelar();
                s.Publicacion!.VolverADisponible();
                avisos.Add((s.Publicacion.PropietarioId, TiposNotificacion.SolicitudCancelada, $"Se canceló una solicitud para \"{s.Publicacion.Titulo}\".", s.Id));
            }
            else
            {
                s.Rechazar("La cuenta del propietario fue eliminada.");
                s.Publicacion!.VolverADisponible();
                avisos.Add((s.SolicitanteId, TiposNotificacion.SolicitudRechazada, $"La publicación \"{s.Publicacion.Titulo}\" ya no está disponible.", s.Id));
            }
        }
        // 2) Publicaciones activas: se retiran del catálogo
        foreach (var p in await _pubs.ListarActivasParaActualizarAsync(actorId)) p.Cancelar("Cuenta eliminada por su titular.");
        // 3) Comentarios públicos: se ocultan (los mensajes privados quedan para la contraparte, sin nombre del autor)
        foreach (var c in await _comentarios.ListarDelAutorAsync(actorId, 10_000)) if (!c.EstaOculto) c.Ocultar("Cuenta eliminada por su titular.");
        // 4) Sesiones y datos personales
        await _refrescos.RevocarTodasDelUsuarioAsync(actorId, ahora);
        u.Anonimizar(ahora);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "CUENTA_ELIMINADA", "Usuario", actorId, null, ahora));
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(actorId);
        _log.LogWarning("Cuenta eliminada (anonimizada) por su titular {UsuarioId}", actorId);

        // 5) Fuera de la transacción: archivos y avisos (si fallan, no deshacen la eliminación)
        try { await _almacen.EliminarDelUsuarioAsync(actorId); }
        catch (Exception ex) { _log.LogError(ex, "No se pudieron borrar los archivos del usuario eliminado {UsuarioId}", actorId); }
        foreach (var a in avisos) await _notificador.NotificarAsync(a.UsuarioId, a.Tipo, a.Mensaje, a.Recurso);
    }
}
