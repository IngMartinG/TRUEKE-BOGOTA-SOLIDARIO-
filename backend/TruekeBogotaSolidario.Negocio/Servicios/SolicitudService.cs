using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface ISolicitudService
{
    Task<SolicitudDto> CrearAsync(Guid actorId, CrearSolicitudRequest r);
    Task<IReadOnlyList<SolicitudDto>> ListarEnviadasAsync(Guid actorId);
    Task<IReadOnlyList<SolicitudDto>> ListarRecibidasAsync(Guid actorId);
    Task<SolicitudDto> AceptarAsync(Guid actorId, Guid solicitudId);
    Task RechazarAsync(Guid actorId, Guid solicitudId, string? motivo);
    Task CancelarAsync(Guid actorId, Guid solicitudId);
}

public sealed class SolicitudService : ISolicitudService
{
    private readonly ISolicitudRepository _solicitudes;
    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly ITransaccionRepository _transacciones;
    private readonly IUnidadDeTrabajo _uow;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly ILogger<SolicitudService> _log;

    public SolicitudService(ISolicitudRepository solicitudes, IPublicacionRepository pubs, IUsuarioRepository usuarios,
        ITransaccionRepository transacciones, IUnidadDeTrabajo uow, TimeProvider reloj, INotificador notificador, ILogger<SolicitudService> log)
    {
        _solicitudes = solicitudes; _pubs = pubs; _usuarios = usuarios; _transacciones = transacciones; _uow = uow; _reloj = reloj;
        _notificador = notificador; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid recursoId)
        => _notificador.NotificarAsync(usuarioId, new NotificacionDto(tipo, mensaje, recursoId, Ahora));

    public async Task<SolicitudDto> CrearAsync(Guid actorId, CrearSolicitudRequest r)
    {
        var solicitante = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var pub = await _pubs.ObtenerPorIdAsync(r.PublicacionId);
        if (pub is null || pub.EstaOculta) throw new NoEncontradoException("Publicación no encontrada.");
        if (pub.PropietarioId == actorId) throw new ReglaDeNegocioException("No puedes solicitar tu propia publicación.");
        if (pub.Estado == EstadoPublicacionEnum.EnNegociacion) throw new ReglaDeNegocioException("La publicación ya está en negociación con otro usuario.");
        if (pub.Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("La publicación ya no está disponible.");
        if (await _solicitudes.ContarPendientesPorSolicitanteAsync(actorId) >= Limites.MaxSolicitudesPendientesPorUsuario)
            throw new ReglaDeNegocioException($"Tienes {Limites.MaxSolicitudesPendientesPorUsuario} solicitudes pendientes. Espera respuesta o cancela alguna.");

        var solicitud = new Solicitud(pub.Id, actorId, r.Mensaje);
        pub.MarcarEnNegociacion(); // RowVersion: si dos personas solicitan a la vez, una recibe 409
        _solicitudes.Agregar(solicitud);
        await _uow.GuardarCambiosAsync();

        await NotificarAsync(pub.PropietarioId, TiposNotificacion.SolicitudNueva, $"Nueva solicitud para \"{pub.Titulo}\".", solicitud.Id);
        return ADto(solicitud, pub, solicitante, verContacto: false, contacto: null, Ahora);
    }

    public async Task<IReadOnlyList<SolicitudDto>> ListarEnviadasAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _solicitudes.ListarPorSolicitanteAsync(actorId);
        var resultado = new List<SolicitudDto>(lista.Count);
        foreach (var s in lista)
        {
            string? contacto = null;
            if (s.Estado == EstadoSolicitud.Aceptada)
                contacto = (await _usuarios.ObtenerPorIdAsync(s.Publicacion!.PropietarioId))?.Correo;
            resultado.Add(ADto(s, s.Publicacion!, s.Solicitante!, contacto is not null, contacto, ahora));
        }
        return resultado;
    }

    public async Task<IReadOnlyList<SolicitudDto>> ListarRecibidasAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _solicitudes.ListarRecibidasAsync(actorId);
        return lista.Select(s => ADto(s, s.Publicacion!, s.Solicitante!, s.Estado == EstadoSolicitud.Aceptada,
            s.Estado == EstadoSolicitud.Aceptada ? s.Solicitante!.Correo : null, ahora)).ToList();
    }

    public async Task<SolicitudDto> AceptarAsync(Guid actorId, Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.Publicacion!.PropietarioId != actorId) throw new NoEncontradoException("Solicitud no encontrada.");
        var pub = s.Publicacion;
        var oferente = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var receptor = s.Solicitante!;
        var ahora = Ahora;

        // Tope anti-farmeo: máximo N transacciones con puntos por persona en una ventana de 24 h
        var desde = ahora.AddHours(-24);
        var puntosOferente = await _transacciones.ContarDesdeAsync(oferente.Id, desde) < PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia;
        var puntosReceptor = await _transacciones.ContarDesdeAsync(receptor.Id, desde) < PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia;

        s.Aceptar();
        pub.ConfirmarIntercambio();
        oferente.RegistrarTransaccionCompletada(pub.Modo, puntosOferente);
        receptor.RegistrarTransaccionCompletada(pub.Modo, puntosReceptor);
        _transacciones.Agregar(new Transaccion(pub.Id, s.Id, oferente.Id, receptor.Id, pub.Modo, puntosOferente, puntosReceptor));

        // Un solo SaveChanges = todo o nada. Con RowVersion, dos "aceptar" simultáneos no duplican puntos (el segundo recibe 409).
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Transacción completada {SolicitudId} modo {Modo}", s.Id, pub.Modo);
        await NotificarAsync(receptor.Id, TiposNotificacion.SolicitudAceptada, $"Tu solicitud para \"{pub.Titulo}\" fue aceptada.", s.Id);
        return ADto(s, pub, receptor, verContacto: true, contacto: receptor.Correo, ahora);
    }

    public async Task RechazarAsync(Guid actorId, Guid solicitudId, string? motivo)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.Publicacion!.PropietarioId != actorId) throw new NoEncontradoException("Solicitud no encontrada.");
        s.Rechazar(motivo ?? "");
        s.Publicacion!.VolverADisponible();
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(s.SolicitanteId, TiposNotificacion.SolicitudRechazada, $"Tu solicitud para \"{s.Publicacion.Titulo}\" fue rechazada.", s.Id);
    }

    public async Task CancelarAsync(Guid actorId, Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.SolicitanteId != actorId) throw new NoEncontradoException("Solicitud no encontrada.");
        s.Cancelar();
        s.Publicacion!.VolverADisponible();
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(s.Publicacion.PropietarioId, TiposNotificacion.SolicitudCancelada, $"Se canceló una solicitud para \"{s.Publicacion.Titulo}\".", s.Id);
    }

    private static SolicitudDto ADto(Solicitud s, Publicacion pub, Usuario solicitante, bool verContacto, string? contacto, DateTime ahora)
        => new(s.Id, pub.Id, pub.Titulo, pub.Modo.ToString(), Mapeos.APerfilPublico(solicitante, ahora), s.FechaSolicitud, s.Mensaje,
            s.Estado.ToString(), s.MotivoRechazo, verContacto ? contacto : null);
}
