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
    /// <summary>El dueño acepta: queda "Aceptada" para coordinar la entrega por chat. Aún NO se otorgan puntos.</summary>
    Task<SolicitudDto> AceptarAsync(Guid actorId, Guid solicitudId);
    Task RechazarAsync(Guid actorId, Guid solicitudId, string? motivo);
    Task CancelarAsync(Guid actorId, Guid solicitudId);
    /// <summary>Cada parte confirma que la entrega ocurrió. Con ambas confirmaciones se completa y se otorgan los Eco-Puntos.</summary>
    Task<SolicitudDto> ConfirmarEntregaAsync(Guid actorId, Guid solicitudId);
    /// <summary>Cualquiera de las partes informa que el intercambio acordado no ocurrió: la publicación vuelve a estar disponible.</summary>
    Task<SolicitudDto> MarcarNoConcretadaAsync(Guid actorId, Guid solicitudId, string motivo);
    Task<CalificacionDto> CalificarAsync(Guid actorId, Guid solicitudId, CalificarRequest r);
    Task<PaginaDto<CalificacionDto>> ListarCalificacionesAsync(Guid usuarioId, int pagina, int tamano);
    /// <summary>Tarea en segundo plano: cierra las aceptadas que vencieron.</summary>
    Task<IReadOnlyList<Guid>> ListarParaCierreAutomaticoAsync(int maximo);
    Task CerrarAutomaticamenteAsync(Guid solicitudId);
}

public sealed class SolicitudService : ISolicitudService
{
    private readonly ISolicitudRepository _solicitudes;
    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly ITransaccionRepository _transacciones;
    private readonly ICalificacionRepository _calificaciones;
    private readonly IUnidadDeTrabajo _uow;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly IConversacionRepository _conversaciones;
    private readonly ILogger<SolicitudService> _log;

    public SolicitudService(ISolicitudRepository solicitudes, IPublicacionRepository pubs, IUsuarioRepository usuarios,
        ITransaccionRepository transacciones, ICalificacionRepository calificaciones, IUnidadDeTrabajo uow, TimeProvider reloj,
        INotificador notificador, IConversacionRepository conversaciones, ILogger<SolicitudService> log)
    {
        _solicitudes = solicitudes; _pubs = pubs; _usuarios = usuarios; _transacciones = transacciones; _calificaciones = calificaciones;
        _uow = uow; _reloj = reloj; _notificador = notificador; _conversaciones = conversaciones; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid recursoId)
        => _notificador.NotificarAsync(usuarioId, tipo, mensaje, recursoId);

    // ---------------- Crear / listar ----------------
    public async Task<SolicitudDto> CrearAsync(Guid actorId, CrearSolicitudRequest r)
    {
        var solicitante = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(solicitante);
        var ahora = Ahora;
        var pub = await _pubs.ObtenerPorIdAsync(r.PublicacionId);
        if (pub is null || pub.EstaOculta || pub.Propietario!.EstaEliminado || pub.Propietario.SuspensionVigente(ahora))
            throw new NoEncontradoException("Publicación no encontrada.");
        if (pub.PropietarioId == actorId) throw new ReglaDeNegocioException("No puedes solicitar tu propia publicación.");
        if (pub.Estado == EstadoPublicacionEnum.EnNegociacion) throw new ReglaDeNegocioException("La publicación ya está en negociación con otro usuario.");
        if (pub.Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("La publicación ya no está disponible.");
        if (await _solicitudes.ContarPendientesPorSolicitanteAsync(actorId) >= Limites.MaxSolicitudesPendientesPorUsuario)
            throw new ReglaDeNegocioException($"Tienes {Limites.MaxSolicitudesPendientesPorUsuario} solicitudes pendientes. Espera respuesta o cancela alguna.");

        var solicitud = new Solicitud(pub.Id, actorId, r.Mensaje);
        pub.MarcarEnNegociacion(); // RowVersion: si dos personas solicitan a la vez, una recibe 409
        _solicitudes.Agregar(solicitud);

        // El chat nace con la solicitud; su mensaje es el primero de la conversación.
        var conversacion = new Conversacion(solicitud.Id, pub.Id, pub.PropietarioId, actorId, ahora);
        _conversaciones.Agregar(conversacion);
        _conversaciones.AgregarMensaje(new Mensaje(conversacion.Id, actorId, solicitud.Mensaje, ahora));
        await _uow.GuardarCambiosAsync();

        await NotificarAsync(pub.PropietarioId, TiposNotificacion.SolicitudNueva, $"Nueva solicitud para \"{pub.Titulo}\".", solicitud.Id);
        return ADto(solicitud, pub, solicitante, actorId, conversacion.Id, puedoCalificar: false, ahora);
    }

    private async Task<IReadOnlyList<SolicitudDto>> MapearAsync(Guid actorId, IReadOnlyList<Solicitud> lista)
    {
        var ahora = Ahora;
        var ids = lista.Select(s => s.Id).ToList();
        var conversaciones = await _conversaciones.IdsPorSolicitudAsync(ids);
        var calificadas = await _calificaciones.SolicitudesCalificadasPorAsync(actorId, ids);
        return lista.Select(s => ADto(s, s.Publicacion!, s.Solicitante!, actorId,
            conversaciones.TryGetValue(s.Id, out var c) ? c : null,
            PuedeCalificar(s, ahora) && !calificadas.Contains(s.Id), ahora)).ToList();
    }

    public async Task<IReadOnlyList<SolicitudDto>> ListarEnviadasAsync(Guid actorId)
        => await MapearAsync(actorId, await _solicitudes.ListarPorSolicitanteAsync(actorId));

    public async Task<IReadOnlyList<SolicitudDto>> ListarRecibidasAsync(Guid actorId)
        => await MapearAsync(actorId, await _solicitudes.ListarRecibidasAsync(actorId));

    private async Task<SolicitudDto> UnaAsync(Guid actorId, Solicitud s) => (await MapearAsync(actorId, new[] { s }))[0];

    // ---------------- Respuesta del dueño ----------------
    private async Task<Solicitud> CargarComoDuenioAsync(Guid actorId, Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.Publicacion!.PropietarioId != actorId) throw new NoEncontradoException("Solicitud no encontrada.");
        return s;
    }

    private async Task<Solicitud> CargarComoParticipanteAsync(Guid actorId, Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || (s.SolicitanteId != actorId && s.Publicacion!.PropietarioId != actorId))
            throw new NoEncontradoException("Solicitud no encontrada.");
        return s;
    }

    public async Task<SolicitudDto> AceptarAsync(Guid actorId, Guid solicitudId)
    {
        var s = await CargarComoDuenioAsync(actorId, solicitudId);
        s.Aceptar(Ahora); // la publicación sigue "EnNegociacion" (reservada) hasta que se complete o no se concrete
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(s.SolicitanteId, TiposNotificacion.SolicitudAceptada,
            $"¡Aceptaron tu solicitud para \"{s.Publicacion!.Titulo}\"! Coordinen la entrega por el chat y confírmala cuando ocurra.", s.Id);
        return await UnaAsync(actorId, s);
    }

    public async Task RechazarAsync(Guid actorId, Guid solicitudId, string? motivo)
    {
        var s = await CargarComoDuenioAsync(actorId, solicitudId);
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

    // ---------------- Entrega ----------------
    public async Task<SolicitudDto> ConfirmarEntregaAsync(Guid actorId, Guid solicitudId)
    {
        var s = await CargarComoParticipanteAsync(actorId, solicitudId);
        var esDuenio = s.Publicacion!.PropietarioId == actorId;
        var ahora = Ahora;
        var ambas = s.ConfirmarEntrega(esDuenio, ahora);
        var otra = esDuenio ? s.SolicitanteId : s.Publicacion.PropietarioId;
        if (ambas)
        {
            await CompletarAsync(s, ahora);
        }
        else
        {
            await _uow.GuardarCambiosAsync();
            await NotificarAsync(otra, TiposNotificacion.EntregaConfirmada,
                $"La otra parte confirmó la entrega de \"{s.Publicacion.Titulo}\". Confírmala tú también (si no, se completará sola en {Limites.DiasCierreConUnaConfirmacion} días).", s.Id);
        }
        return await UnaAsync(actorId, s);
    }

    public async Task<SolicitudDto> MarcarNoConcretadaAsync(Guid actorId, Guid solicitudId, string motivo)
    {
        var s = await CargarComoParticipanteAsync(actorId, solicitudId);
        if (s.ConfirmadaPorDuenioUtc is not null || s.ConfirmadaPorSolicitanteUtc is not null)
            throw new ReglaDeNegocioException("Alguien ya confirmó la entrega. Si hay un problema, repórtalo con una denuncia.");
        s.MarcarNoConcretada(motivo, Ahora);
        s.Publicacion!.VolverADisponible();
        await _uow.GuardarCambiosAsync();
        var otra = s.Publicacion.PropietarioId == actorId ? s.SolicitanteId : s.Publicacion.PropietarioId;
        await NotificarAsync(otra, TiposNotificacion.IntercambioNoConcretado, $"El intercambio de \"{s.Publicacion.Titulo}\" se marcó como no concretado.", s.Id);
        return await UnaAsync(actorId, s);
    }

    /// <summary>Completa el intercambio: aquí (y solo aquí) se otorgan Eco-Puntos y reputación, con el tope anti-farmeo.</summary>
    private async Task CompletarAsync(Solicitud s, DateTime ahora)
    {
        var pub = s.Publicacion!;
        var oferente = pub.Propietario ?? await _usuarios.ObtenerPorIdAsync(pub.PropietarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
        var receptor = s.Solicitante ?? await _usuarios.ObtenerPorIdAsync(s.SolicitanteId) ?? throw new NoEncontradoException("Usuario no encontrado.");

        // Anti-farmeo 1: máximo N transacciones con puntos por persona en una ventana de 24 h.
        var desde = ahora.AddHours(-24);
        var puntosOferente = await _transacciones.ContarDesdeAsync(oferente.Id, desde) < PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia;
        var puntosReceptor = await _transacciones.ContarDesdeAsync(receptor.Id, desde) < PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia;

        // Anti-farmeo 2: la misma pareja solo suma puntos y reputación una vez por ventana. Con dos cuentas propias
        // (o con un amigo) no se puede "fabricar" saldo para descuentos ni una reputación falsa para estafar.
        var parejaRepetida = await _transacciones.ExisteConPuntosEntreDesdeAsync(oferente.Id, receptor.Id,
            ahora.AddDays(-PoliticaEcoPuntos.DiasEntreTransaccionesConPuntosMismaPareja));
        if (parejaRepetida)
        {
            puntosOferente = false;
            puntosReceptor = false;
        }

        s.Completar(ahora);
        pub.ConfirmarIntercambio();
        oferente.RegistrarTransaccionCompletada(pub.Modo, puntosOferente);
        receptor.RegistrarTransaccionCompletada(pub.Modo, puntosReceptor);
        _transacciones.Agregar(new Transaccion(pub.Id, s.Id, oferente.Id, receptor.Id, pub.Modo, puntosOferente, puntosReceptor, ahora));

        // Un solo SaveChanges = todo o nada. Con RowVersion, dos confirmaciones simultáneas no duplican puntos (la segunda recibe 409).
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Transacción completada {SolicitudId} modo {Modo}", s.Id, pub.Modo);
        foreach (var id in new[] { oferente.Id, receptor.Id })
            await NotificarAsync(id, TiposNotificacion.IntercambioCompletado, parejaRepetida
                ? $"¡Intercambio de \"{pub.Titulo}\" completado! Como ya intercambiaron hace poco, este no suma Eco-Puntos (se suman una vez cada {PoliticaEcoPuntos.DiasEntreTransaccionesConPuntosMismaPareja} días por pareja)."
                : $"¡Intercambio de \"{pub.Titulo}\" completado! Ya puedes calificar a la otra persona.", s.Id);
    }

    public async Task<IReadOnlyList<Guid>> ListarParaCierreAutomaticoAsync(int maximo)
    {
        var ahora = Ahora;
        return await _solicitudes.ListarParaCierreAutomaticoAsync(ahora.AddDays(-Limites.DiasCierreConUnaConfirmacion),
            ahora.AddDays(-Limites.DiasCierreSinConfirmacion), maximo);
    }

    public async Task CerrarAutomaticamenteAsync(Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.Estado != EstadoSolicitud.Aceptada || s.FechaAceptacionUtc is null) return;
        var ahora = Ahora;
        var algunaConfirmacion = s.ConfirmadaPorDuenioUtc is not null || s.ConfirmadaPorSolicitanteUtc is not null;
        if (algunaConfirmacion && s.FechaAceptacionUtc < ahora.AddDays(-Limites.DiasCierreConUnaConfirmacion))
        {
            await CompletarAsync(s, ahora);
        }
        else if (!algunaConfirmacion && s.FechaAceptacionUtc < ahora.AddDays(-Limites.DiasCierreSinConfirmacion))
        {
            s.MarcarNoConcretada($"Nadie confirmó la entrega en {Limites.DiasCierreSinConfirmacion} días.", ahora);
            s.Publicacion!.VolverADisponible();
            await _uow.GuardarCambiosAsync();
            foreach (var id in new[] { s.SolicitanteId, s.Publicacion.PropietarioId })
                await NotificarAsync(id, TiposNotificacion.IntercambioNoConcretado,
                    $"El intercambio de \"{s.Publicacion.Titulo}\" se cerró sin confirmación de entrega.", s.Id);
        }
    }

    // ---------------- Calificaciones ----------------
    private static bool PuedeCalificar(Solicitud s, DateTime ahora)
        => s.Estado == EstadoSolicitud.Completada && s.FechaCierreUtc > ahora.AddDays(-Calificacion.DiasParaCalificar);

    public async Task<CalificacionDto> CalificarAsync(Guid actorId, Guid solicitudId, CalificarRequest r)
    {
        var s = await CargarComoParticipanteAsync(actorId, solicitudId);
        var ahora = Ahora;
        if (s.Estado != EstadoSolicitud.Completada) throw new ReglaDeNegocioException("Solo se califica un intercambio completado.");
        if (!PuedeCalificar(s, ahora)) throw new ReglaDeNegocioException($"El plazo para calificar ({Calificacion.DiasParaCalificar} días) ya venció.");
        if (await _calificaciones.ExisteAsync(s.Id, actorId)) throw new ConflictoDeConcurrenciaException("Ya calificaste este intercambio.");

        var autor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var calificadoId = s.Publicacion!.PropietarioId == actorId ? s.SolicitanteId : s.Publicacion.PropietarioId;
        var calificado = await _usuarios.ObtenerPorIdAsync(calificadoId) ?? throw new NoEncontradoException("Usuario no encontrado.");

        // Solo la primera calificación a la misma persona en la ventana suma al promedio (anti-inflado de reputación).
        var cuenta = !await _calificaciones.ExisteContadaEntreDesdeAsync(actorId, calificadoId,
            ahora.AddDays(-PoliticaEcoPuntos.DiasEntreCalificacionesMismaPareja));
        var c = new Calificacion(s.Id, actorId, calificadoId, r.Estrellas, r.Comentario, ahora, cuenta);
        _calificaciones.Agregar(c);
        if (cuenta) calificado.RegistrarCalificacion(r.Estrellas);
        await _uow.GuardarCambiosAsync(); // índice único (solicitud, autor): un doble envío simultáneo → 409
        await NotificarAsync(calificadoId, TiposNotificacion.CalificacionRecibida, $"Recibiste una calificación de {r.Estrellas} estrella(s).", s.Id);
        return new CalificacionDto(c.Id, Mapeos.APerfilPublico(autor, ahora), c.Estrellas, c.Comentario, c.FechaUtc);
    }

    public async Task<PaginaDto<CalificacionDto>> ListarCalificacionesAsync(Guid usuarioId, int pagina, int tamano)
    {
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        var ahora = Ahora;
        if (u is null || u.EstaEliminado || u.SuspensionVigente(ahora)) throw new NoEncontradoException("Usuario no encontrado.");
        var (items, total) = await _calificaciones.ListarRecibidasAsync(usuarioId, pagina, tamano);
        var dtos = items.Select(c => new CalificacionDto(c.Id, Mapeos.APerfilPublico(c.Autor!, ahora), c.Estrellas,
            c.ComentarioOculto ? null : c.Comentario, c.FechaUtc)).ToList();
        return new PaginaDto<CalificacionDto>(dtos, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    private static SolicitudDto ADto(Solicitud s, Publicacion pub, Usuario solicitante, Guid actorId, Guid? conversacionId, bool puedoCalificar, DateTime ahora)
    {
        DateTime? cierre = null;
        if (s.Estado == EstadoSolicitud.Aceptada && s.FechaAceptacionUtc is { } aceptada)
            cierre = aceptada.AddDays(s.ConfirmadaPorDuenioUtc is not null || s.ConfirmadaPorSolicitanteUtc is not null
                ? Limites.DiasCierreConUnaConfirmacion : Limites.DiasCierreSinConfirmacion);
        return new(s.Id, pub.Id, pub.Titulo, pub.Modo.ToString(), Mapeos.APerfilPublico(pub.Propietario!, ahora),
            Mapeos.APerfilPublico(solicitante, ahora), pub.PropietarioId == actorId, s.FechaSolicitud, s.Mensaje, s.Estado.ToString(),
            s.MotivoRechazo, conversacionId, s.FechaAceptacionUtc, s.ConfirmadaPorDuenioUtc is not null, s.ConfirmadaPorSolicitanteUtc is not null,
            s.FechaCierreUtc, cierre, puedoCalificar);
    }
}
