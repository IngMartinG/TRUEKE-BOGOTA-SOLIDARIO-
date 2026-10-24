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
    /// <summary>Quien debe responder acepta la propuesta vigente: queda "Aceptada" para coordinar la entrega por chat. Aún NO se otorgan puntos.</summary>
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
    /// <summary>Tarea en segundo plano: aceptadas a las que les toca un recordatorio de entrega o de cierre.</summary>
    Task<IReadOnlyList<Guid>> ListarParaRecordatorioAsync(int maximo);
    /// <summary>Recuerda confirmar la entrega a quien aún no lo hizo (cada recordatorio sale una sola vez).</summary>
    Task RecordarEntregaAsync(Guid solicitudId);

    /// <summary>
    /// La publicación deja de estar disponible por fuera de un trueke (su dueño la retiró o moderación la ocultó): se cierran
    /// los truekes donde se OFRECÍA. Si estaba reservada como objeto ofrecido (y <paramref name="incluirAceptadas"/>), ese
    /// trueke queda no concretado y la otra publicación vuelve al catálogo. NO guarda: el llamador guarda todo junto y
    /// después envía los avisos devueltos.
    /// </summary>
    Task<AvisosDiferidos> RetirarOfertasAsync(Guid publicacionId, string motivo, bool incluirAceptadas);
    /// <summary>Un trueke aceptado no se concretó: los objetos ofrecidos (menos <paramref name="exceptoId"/>) vuelven al catálogo. NO guarda.</summary>
    Task<AvisosDiferidos> LiberarOfrecidasAsync(Solicitud noConcretada, Guid? exceptoId = null);
    /// <summary>
    /// Quien debe responder la propuesta vigente la reemplaza: otro precio (Compra) u otros objetos de quien solicita (Trueke).
    /// Pasa el turno a la otra parte; máximo <see cref="Solicitud.MaxContraofertas"/> por solicitud.
    /// </summary>
    Task<SolicitudDto> ContraofertarAsync(Guid actorId, Guid solicitudId, ContraofertaRequest r);
}

/// <summary>Avisos que se envían DESPUÉS de guardar (si el guardado falla, nadie recibe un aviso falso).</summary>
public sealed class AvisosDiferidos
{
    private readonly List<(Guid Usuario, string Tipo, string Mensaje, Guid Recurso)> _avisos = new();
    public void Agregar(Guid usuarioId, string tipo, string mensaje, Guid recursoId) => _avisos.Add((usuarioId, tipo, mensaje, recursoId));
    public void Agregar(AvisosDiferidos otros) => _avisos.AddRange(otros._avisos);
    public async Task EnviarAsync(INotificador notificador)
    {
        foreach (var a in _avisos) await notificador.NotificarAsync(a.Usuario, a.Tipo, a.Mensaje, a.Recurso);
    }
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
    private readonly IBloqueoRepository _bloqueos;
    private readonly IMovimientoEcoPuntosRepository _movimientos;
    private readonly ILogger<SolicitudService> _log;

    public SolicitudService(ISolicitudRepository solicitudes, IPublicacionRepository pubs, IUsuarioRepository usuarios,
        ITransaccionRepository transacciones, ICalificacionRepository calificaciones, IUnidadDeTrabajo uow, TimeProvider reloj,
        INotificador notificador, IConversacionRepository conversaciones, IBloqueoRepository bloqueos, IMovimientoEcoPuntosRepository movimientos,
        ILogger<SolicitudService> log)
    {
        _solicitudes = solicitudes; _pubs = pubs; _usuarios = usuarios; _transacciones = transacciones; _calificaciones = calificaciones;
        _uow = uow; _reloj = reloj; _notificador = notificador; _conversaciones = conversaciones; _bloqueos = bloqueos; _movimientos = movimientos; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid recursoId)
        => _notificador.NotificarAsync(usuarioId, tipo, mensaje, recursoId);

    // ---------------- Crear / listar ----------------
    public async Task<SolicitudDto> CrearAsync(Guid actorId, CrearSolicitudRequest r)
    {
        var solicitante = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCuentaCompleta(solicitante);
        var ahora = Ahora;
        var pub = await _pubs.ObtenerPorIdAsync(r.PublicacionId);
        if (pub is null || pub.EstaOculta || pub.Propietario!.EstaEliminado || pub.Propietario.SuspensionVigente(ahora))
            throw new NoEncontradoException("Publicación no encontrada.");
        if (pub.PropietarioId == actorId) throw new ReglaDeNegocioException("No puedes solicitar tu propia publicación.");
        if (await _bloqueos.ExisteEntreAsync(actorId, pub.PropietarioId))
            throw new ReglaDeNegocioException("No puedes solicitar esta publicación.");
        if (pub.Estado == EstadoPublicacionEnum.EnNegociacion)
            throw new ReglaDeNegocioException("El dueño está concretando con otra persona. Guárdala en favoritos: si no se concreta, vuelve a estar disponible.");
        if (pub.Estado != EstadoPublicacionEnum.Disponible) throw new ReglaDeNegocioException("La publicación ya no está disponible.");
        if (await _solicitudes.ExisteEnCursoAsync(pub.Id, actorId))
            throw new ReglaDeNegocioException("Ya enviaste una solicitud para esta publicación. Sigue la conversación en Mensajes.");
        if (await _solicitudes.ContarPendientesPorSolicitanteAsync(actorId) >= Limites.MaxSolicitudesPendientesPorUsuario)
            throw new ReglaDeNegocioException($"Tienes {Limites.MaxSolicitudesPendientesPorUsuario} solicitudes pendientes. Espera respuesta o cancela alguna.");

        var ids = (r.PublicacionesOfrecidasIds ?? new List<Guid>())
            .Concat(r.PublicacionOfrecidaId is { } una ? new[] { una } : Array.Empty<Guid>()).Distinct().ToList();
        var ofrecidas = await ValidarOfrecidasAsync(ids, pub, actorId);
        if (r.PrecioPropuestoCop is not null && pub.Modo != ModoTransaccion.Compra)
            throw new ReglaDeNegocioException("Solo en una compra se propone un precio.");
        var precio = r.PrecioPropuestoCop == pub.PrecioReferenciaCop ? null : r.PrecioPropuestoCop;

        // Varias personas pueden estar interesadas a la vez: la publicación sigue en el catálogo hasta que el dueño elija a una.
        // Los objetos ofrecidos también siguen disponibles (se pueden ofrecer en varios truekes) hasta que alguno se acepte.
        var solicitud = new Solicitud(pub.Id, actorId, r.Mensaje, ofrecidas.Select(o => o.Id).ToList(), precio);
        _solicitudes.Agregar(solicitud);

        // El chat nace con la solicitud; su mensaje es el primero de la conversación.
        var conversacion = new Conversacion(solicitud.Id, pub.Id, pub.PropietarioId, actorId, ahora);
        _conversaciones.Agregar(conversacion);
        _conversaciones.AgregarMensaje(new Mensaje(conversacion.Id, actorId, solicitud.Mensaje, ahora));
        await _uow.GuardarCambiosAsync();

        await NotificarAsync(pub.PropietarioId, TiposNotificacion.SolicitudNueva, ofrecidas.Count > 0
            ? $"Nueva propuesta de trueke para \"{pub.Titulo}\": te ofrecen {Titulos(ofrecidas)}."
            : precio is { } p ? $"Nueva solicitud para \"{pub.Titulo}\": te ofrecen {Cop(p)}." : $"Nueva solicitud para \"{pub.Titulo}\".", solicitud.Id);
        return ADto(solicitud, pub, solicitante, actorId, conversacion.Id, puedoCalificar: false, ahora, ofrecidas);
    }

    /// <summary>
    /// Lo que se ofrece en un trueke: publicaciones de QUIEN SOLICITA, disponibles y visibles, hasta 3 (lista vacía = nada).
    /// En una contraoferta del dueño también son de quien solicita: elige cuáles de sus objetos prefiere.
    /// </summary>
    private async Task<IReadOnlyList<Publicacion>> ValidarOfrecidasAsync(IReadOnlyList<Guid> ids, Publicacion pedida, Guid solicitanteId)
    {
        if (ids.Count == 0) return Array.Empty<Publicacion>();
        if (pedida.Modo != ModoTransaccion.Trueke)
            throw new ReglaDeNegocioException("Solo en un trueke se ofrecen objetos a cambio.");
        if (ids.Count > Solicitud.MaxObjetosOfrecidos)
            throw new ReglaDeNegocioException($"Puedes ofrecer hasta {Solicitud.MaxObjetosOfrecidos} objetos.");
        var lista = new List<Publicacion>();
        foreach (var id in ids.Distinct())
        {
            var o = await _pubs.ObtenerPorIdAsync(id);
            if (o is null || o.PropietarioId != solicitanteId) throw new NoEncontradoException("No encontramos uno de los objetos ofrecidos.");
            if (o.Id == pedida.Id || o.Estado != EstadoPublicacionEnum.Disponible || o.EstaOculta)
                throw new ReglaDeNegocioException($"\"{o.Titulo}\" no está disponible para ofrecerlo: debe estar publicado y sin reservar.");
            lista.Add(o);
        }
        return lista;
    }

    private static string Titulos(IEnumerable<Publicacion> pubs) => string.Join(" + ", pubs.Select(p => $"\"{p.Titulo}\""));

    private static readonly System.Globalization.CultureInfo Colombia = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
    private static string Cop(decimal valor) => "$" + valor.ToString("N0", Colombia);

    // ---------------- Contraofertas ----------------
    public async Task<SolicitudDto> ContraofertarAsync(Guid actorId, Guid solicitudId, ContraofertaRequest r)
    {
        var s = await CargarComoParticipanteAsync(actorId, solicitudId);
        var pub = s.Publicacion!;
        var esDuenio = pub.PropietarioId == actorId;
        var actor = esDuenio ? pub.Propietario! : s.Solicitante!;
        Guardas.ExigirCuentaCompleta(actor);
        if (s.Estado != EstadoSolicitud.Pendiente) throw new ReglaDeNegocioException("Solo se negocia una solicitud pendiente.");
        if (pub.Estado != EstadoPublicacionEnum.Disponible)
            throw new ReglaDeNegocioException("La publicación está reservada para otra persona: por ahora no se puede negociar.");

        var ids = (r.PublicacionesOfrecidasIds ?? new List<Guid>()).Distinct().ToList();
        IReadOnlyList<Publicacion> ofrecidas = Array.Empty<Publicacion>();
        decimal? precio = null;
        string resumen;
        switch (pub.Modo)
        {
            case ModoTransaccion.Trueke:
                if (r.PrecioPropuestoCop is not null) throw new ReglaDeNegocioException("En un trueke se proponen objetos, no dinero.");
                if (ids.Count == 0) throw new ReglaDeNegocioException("Elige al menos un objeto para la contraoferta.");
                if (ids.ToHashSet().SetEquals(s.Ofrecidas.Select(o => o.PublicacionId)))
                    throw new ReglaDeNegocioException("Esa es la misma propuesta: si te sirve, acéptala.");
                ofrecidas = await ValidarOfrecidasAsync(ids, pub, s.SolicitanteId);
                resumen = esDuenio ? $"prefiere {Titulos(ofrecidas)} a cambio" : $"ahora ofrece {Titulos(ofrecidas)}";
                break;
            case ModoTransaccion.Compra:
                if (ids.Count > 0) throw new ReglaDeNegocioException("En una compra se propone un precio, no objetos.");
                precio = r.PrecioPropuestoCop ?? throw new ReglaDeNegocioException("Escribe el precio que propones.");
                if (precio == (s.PrecioPropuestoCop ?? pub.PrecioReferenciaCop))
                    throw new ReglaDeNegocioException("Ese es el mismo precio: si te sirve, acepta la propuesta.");
                resumen = $"propone {Cop(precio.Value)}";
                if (precio == pub.PrecioReferenciaCop) precio = null; // volver al publicado
                break;
            default:
                throw new ReglaDeNegocioException("En una donación no hay contraofertas: acepta o conversa por el chat.");
        }

        s.Contraofertar(esDuenio, ofrecidas.Select(o => o.Id).ToList(), precio);
        await _uow.GuardarCambiosAsync();
        var otra = esDuenio ? s.SolicitanteId : pub.PropietarioId;
        var restantes = Solicitud.MaxContraofertas - s.Contraofertas;
        await NotificarAsync(otra, TiposNotificacion.SolicitudContraoferta,
            $"Contraoferta por \"{pub.Titulo}\": {Mapeos.NombrePublico(actor.NombreCompleto)} {resumen}. Acéptala, contraoferta o retírate"
            + (restantes > 0 ? "." : " (ya no quedan más contraofertas)."), s.Id);
        return await UnaAsync(actorId, s);
    }

    private async Task<IReadOnlyList<SolicitudDto>> MapearAsync(Guid actorId, IReadOnlyList<Solicitud> lista)
    {
        var ahora = Ahora;
        var ids = lista.Select(s => s.Id).ToList();
        var conversaciones = await _conversaciones.IdsPorSolicitudAsync(ids);
        var calificadas = await _calificaciones.SolicitudesCalificadasPorAsync(actorId, ids);
        return lista.Select(s => ADto(s, s.Publicacion!, s.Solicitante!, actorId,
            conversaciones.TryGetValue(s.Id, out var c) ? c : null,
            PuedeCalificar(s, ahora) && !calificadas.Contains(s.Id), ahora, Ofrecidas(s))).ToList();
    }

    public async Task<IReadOnlyList<SolicitudDto>> ListarEnviadasAsync(Guid actorId)
        => await MapearAsync(actorId, await _solicitudes.ListarPorSolicitanteAsync(actorId));

    public async Task<IReadOnlyList<SolicitudDto>> ListarRecibidasAsync(Guid actorId)
        => await MapearAsync(actorId, await _solicitudes.ListarRecibidasAsync(actorId));

    private async Task<SolicitudDto> UnaAsync(Guid actorId, Solicitud s) => (await MapearAsync(actorId, new[] { s }))[0];

    /// <summary>Los objetos de la propuesta vigente, en el orden en que se eligieron (requiere Ofrecidas.Publicacion cargada).</summary>
    private static IReadOnlyList<Publicacion> Ofrecidas(Solicitud s)
        => s.Ofrecidas.OrderBy(o => o.Orden).Select(o => o.Publicacion).OfType<Publicacion>().ToList();

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

    /// <summary>
    /// Acepta la propuesta vigente quien debe responderla: el dueño la solicitud (o la contraoferta de quien solicita), o
    /// quien solicita la contraoferta del dueño. En los dos casos el resultado es el mismo: el acuerdo queda Aceptado.
    /// </summary>
    public async Task<SolicitudDto> AceptarAsync(Guid actorId, Guid solicitudId)
    {
        var s = await CargarComoParticipanteAsync(actorId, solicitudId);
        var pub = s.Publicacion!;
        var esDuenio = pub.PropietarioId == actorId;
        Guardas.ExigirCuentaCompleta(pub.Propietario!); // quien entrega también debe mostrar su cara (publicaciones anteriores a la regla)
        if (!esDuenio) Guardas.ExigirCuentaCompleta(s.Solicitante!);
        if (pub.Estado == EstadoPublicacionEnum.EnNegociacion)
            throw new ReglaDeNegocioException(esDuenio
                ? "Ya elegiste a otra persona para esta publicación. Si no se concreta, podrás elegir a alguien de la lista de espera."
                : "El dueño está concretando con otra persona. Si no se concreta, podrás aceptar.");
        var ofrecidas = Ofrecidas(s);
        if (ofrecidas.FirstOrDefault(o => o.Estado != EstadoPublicacionEnum.Disponible || o.EstaOculta) is { } noDisponible)
            throw new ReglaDeNegocioException($"\"{noDisponible.Titulo}\" ya no está disponible. Contraoferta con otros objetos, retírate o conversen por el chat.");

        // Aceptar reserva la publicación y la saca del catálogo. RowVersion: dos aceptaciones simultáneas → una recibe 409.
        s.Aceptar(esDuenio, Ahora);
        pub.MarcarEnNegociacion();
        var avisos = new AvisosDiferidos();
        var precio = s.PrecioPropuestoCop is { } p ? $" por {Cop(p)}" : "";
        if (esDuenio)
            avisos.Agregar(s.SolicitanteId, TiposNotificacion.SolicitudAceptada, ofrecidas.Count == 0
                ? $"¡Aceptaron tu solicitud para \"{pub.Titulo}\"{precio}! Coordinen la entrega por el chat y confírmala cuando ocurra."
                : $"¡Aceptaron tu trueke: {Titulos(ofrecidas)} por \"{pub.Titulo}\"! Todos los objetos quedan reservados. Coordinen la entrega por el chat.", s.Id);
        else
            avisos.Agregar(pub.PropietarioId, TiposNotificacion.SolicitudAceptada,
                $"¡Aceptaron tu contraoferta por \"{pub.Titulo}\"{precio}! Queda reservada: coordinen la entrega por el chat.", s.Id);
        await PonerEnEsperaAsync(pub, exceptoId: s.Id, avisos);
        // Un objeto reservado ya no puede entregarse en otro trueke: se cierran las propuestas donde se ofrecía.
        await CancelarOfertasPendientesAsync(pub.Id, s.Id, $"\"{pub.Titulo}\" quedó reservado en otro intercambio.", avisos);

        foreach (var ofrecida in ofrecidas)
        {
            // Los objetos ofrecidos también se reservan: salen del catálogo y quienes los pidieron quedan en lista de espera.
            ofrecida.MarcarEnNegociacion();
            await PonerEnEsperaAsync(ofrecida, exceptoId: null, avisos);
            await CancelarOfertasPendientesAsync(ofrecida.Id, s.Id, $"Ofreciste \"{ofrecida.Titulo}\" en otro trueke que ya fue aceptado.", avisos);
        }
        await _uow.GuardarCambiosAsync();
        await avisos.EnviarAsync(_notificador);
        return await UnaAsync(actorId, s);
    }

    /// <summary>Las solicitudes pendientes de una publicación que acaba de reservarse quedan en lista de espera (se avisa).</summary>
    private async Task PonerEnEsperaAsync(Publicacion pub, Guid? exceptoId, AvisosDiferidos avisos)
    {
        foreach (var otra in (await _solicitudes.ListarEnCursoPorPublicacionAsync(pub.Id, incluirAceptada: false)).Where(x => x.Id != exceptoId))
            avisos.Agregar(otra.SolicitanteId, TiposNotificacion.SolicitudEnEspera,
                $"El dueño de \"{pub.Titulo}\" está concretando con otra persona. Tu solicitud queda en lista de espera: si no se concreta, podrá elegirte.", otra.Id);
    }

    /// <summary>Cancela (con aviso a ambas partes) las propuestas PENDIENTES donde esa publicación se ofrecía a cambio.</summary>
    private async Task CancelarOfertasPendientesAsync(Guid publicacionId, Guid? exceptoId, string motivo, AvisosDiferidos avisos)
    {
        foreach (var o in (await _solicitudes.ListarEnCursoQueOfrecenAsync(publicacionId, incluirAceptada: false)).Where(x => x.Id != exceptoId))
        {
            o.Cancelar(motivo);
            avisos.Agregar(o.Publicacion!.PropietarioId, TiposNotificacion.SolicitudCancelada,
                $"Se retiró una propuesta de trueke para \"{o.Publicacion.Titulo}\": el objeto ofrecido ya no está disponible.", o.Id);
            avisos.Agregar(o.SolicitanteId, TiposNotificacion.SolicitudCancelada,
                $"Se retiró tu propuesta de trueke para \"{o.Publicacion.Titulo}\". {motivo}", o.Id);
        }
    }

    public async Task<AvisosDiferidos> RetirarOfertasAsync(Guid publicacionId, string motivo, bool incluirAceptadas)
    {
        var avisos = new AvisosDiferidos();
        await CancelarOfertasPendientesAsync(publicacionId, null, motivo, avisos);
        if (!incluirAceptadas) return avisos;
        foreach (var s in await _solicitudes.ListarEnCursoQueOfrecenAsync(publicacionId, incluirAceptada: true))
        {
            if (s.Estado != EstadoSolicitud.Aceptada) continue;
            // Ese objeto ofrecido ya no se entregará: el trueke no se concreta y lo pedido (y los demás objetos) vuelven al catálogo.
            s.MarcarNoConcretada(motivo, Ahora);
            s.Publicacion!.VolverADisponible();
            avisos.Agregar(s.Publicacion.PropietarioId, TiposNotificacion.IntercambioNoConcretado,
                $"El trueke por \"{s.Publicacion.Titulo}\" no se concretará: {motivo} Tu publicación volvió al catálogo.", s.Id);
            await AgregarAvisosListaDeEsperaAsync(s.Publicacion, avisos);
            avisos.Agregar(await LiberarOfrecidasAsync(s, exceptoId: publicacionId));
        }
        return avisos;
    }

    public async Task<AvisosDiferidos> LiberarOfrecidasAsync(Solicitud noConcretada, Guid? exceptoId = null)
    {
        var avisos = new AvisosDiferidos();
        foreach (var ofrecida in Ofrecidas(noConcretada).Where(o => o.Id != exceptoId && o.Estado == EstadoPublicacionEnum.EnNegociacion))
        {
            ofrecida.VolverADisponible();
            await AgregarAvisosListaDeEsperaAsync(ofrecida, avisos);
        }
        return avisos;
    }

    public async Task RechazarAsync(Guid actorId, Guid solicitudId, string? motivo)
    {
        var s = await CargarComoDuenioAsync(actorId, solicitudId);
        s.Rechazar(motivo ?? ""); // solo pendientes: la publicación no cambia (sigue disponible o reservada para otra persona)
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(s.SolicitanteId, TiposNotificacion.SolicitudRechazada, $"Tu solicitud para \"{s.Publicacion!.Titulo}\" fue rechazada.", s.Id);
    }

    public async Task CancelarAsync(Guid actorId, Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.SolicitanteId != actorId) throw new NoEncontradoException("Solicitud no encontrada.");
        s.Cancelar(); // solo pendientes: la publicación no cambia
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(s.Publicacion!.PropietarioId, TiposNotificacion.SolicitudCancelada, $"Se canceló una solicitud para \"{s.Publicacion.Titulo}\".", s.Id);
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
                $"La otra parte confirmó la entrega de \"{s.Publicacion.Titulo}\". Confírmala tú también o, si no se concretó, márcalo así (si no haces nada, se completará sola en {Limites.DiasCierreConUnaConfirmacion} días).", s.Id);
        }
        return await UnaAsync(actorId, s);
    }

    public async Task<SolicitudDto> MarcarNoConcretadaAsync(Guid actorId, Guid solicitudId, string motivo)
    {
        var s = await CargarComoParticipanteAsync(actorId, solicitudId);
        var esDuenio = s.Publicacion!.PropietarioId == actorId;
        // Solo quien YA confirmó la entrega no puede desdecirse. La otra parte sí puede indicar que no se concretó: si no,
        // una sola persona podía confirmar y dejar que el intercambio se completara solo (con puntos y reputación para ambos).
        if ((esDuenio ? s.ConfirmadaPorDuenioUtc : s.ConfirmadaPorSolicitanteUtc) is not null)
            throw new ReglaDeNegocioException("Ya confirmaste la entrega. Si hay un problema, repórtalo con una denuncia.");
        var otraConfirmo = (esDuenio ? s.ConfirmadaPorSolicitanteUtc : s.ConfirmadaPorDuenioUtc) is not null;
        s.MarcarNoConcretada(motivo, Ahora);
        var avisos = await LiberarPublicacionesAsync(s);
        var otra = esDuenio ? s.SolicitanteId : s.Publicacion.PropietarioId;
        await _uow.GuardarCambiosAsync();
        if (otraConfirmo) _log.LogInformation("Intercambio {SolicitudId} disputado: una parte confirmó y la otra no", s.Id);
        await NotificarAsync(otra, TiposNotificacion.IntercambioNoConcretado, otraConfirmo
            ? $"La otra parte indicó que el intercambio de \"{s.Publicacion.Titulo}\" no se concretó. Si no es así, repórtalo con una denuncia."
            : $"El intercambio de \"{s.Publicacion.Titulo}\" se marcó como no concretado.", s.Id);
        await avisos.EnviarAsync(_notificador);
        return await UnaAsync(actorId, s);
    }

    /// <summary>
    /// El intercambio aceptado no ocurrió: la publicación (y el objeto ofrecido, si era un trueke con objeto) vuelve al
    /// catálogo. Devuelve los avisos para las listas de espera, que se envían DESPUÉS de guardar.
    /// </summary>
    private async Task<AvisosDiferidos> LiberarPublicacionesAsync(Solicitud noConcretada)
    {
        var avisos = new AvisosDiferidos();
        noConcretada.Publicacion!.VolverADisponible();
        await AgregarAvisosListaDeEsperaAsync(noConcretada.Publicacion, avisos);
        avisos.Agregar(await LiberarOfrecidasAsync(noConcretada));
        return avisos;
    }

    private async Task AgregarAvisosListaDeEsperaAsync(Publicacion pub, AvisosDiferidos avisos)
    {
        var enEspera = await _solicitudes.ListarEnCursoPorPublicacionAsync(pub.Id, incluirAceptada: false);
        foreach (var s in enEspera)
            avisos.Agregar(s.SolicitanteId, TiposNotificacion.SolicitudDisponibleDeNuevo,
                $"\"{pub.Titulo}\" vuelve a estar disponible y tu solicitud sigue en pie: el dueño ahora puede elegirte.", s.Id);
        if (enEspera.Count > 0)
            avisos.Agregar(pub.PropietarioId, TiposNotificacion.SolicitudNueva,
                $"\"{pub.Titulo}\" volvió al catálogo. Tienes {enEspera.Count} {(enEspera.Count == 1 ? "persona" : "personas")} en lista de espera: puedes elegir a otra.", pub.Id);
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

        // Estado de cada persona antes de sumar puntos, para avisar a quien suba de nivel (quien invitó se agrega al recompensarlo).
        var avisosPuntos = new AvisosEcoPuntos();
        avisosPuntos.Observar(oferente);
        avisosPuntos.Observar(receptor);
        s.Completar(ahora);
        pub.ConfirmarIntercambio();
        // Trueke con objetos: los ofrecidos también cambiaron de manos (es un solo intercambio: los puntos se suman una vez).
        var ofrecidas = Ofrecidas(s);
        foreach (var o in ofrecidas.Where(o => o.Estado == EstadoPublicacionEnum.EnNegociacion)) o.ConfirmarIntercambio();
        oferente.RegistrarTransaccionCompletada(pub.Modo, puntosOferente, s.Id, pub.Titulo);
        receptor.RegistrarTransaccionCompletada(pub.Modo, puntosReceptor, s.Id, pub.Titulo);
        // Primera publicación: si una publicación propia cambió de manos (con puntos) antes de cumplir 24 h activa.
        if (puntosOferente) oferente.RecibirBonoPrimeraPublicacion(pub.Id, pub.Titulo);
        if (puntosReceptor && ofrecidas.Count > 0) receptor.RecibirBonoPrimeraPublicacion(ofrecidas[0].Id, ofrecidas[0].Titulo);
        var invitaciones = new List<(Guid Invitado, Guid? Invitador)>();
        foreach (var (invitado, otro, conPuntos) in new[] { (oferente, receptor, puntosOferente), (receptor, oferente, puntosReceptor) })
            if (await RecompensarInvitacionAsync(invitado, otro, conPuntos, s.Id, ahora, avisosPuntos) is { } recompensa)
                invitaciones.Add(recompensa);
        _transacciones.Agregar(new Transaccion(pub.Id, s.Id, oferente.Id, receptor.Id, pub.Modo, puntosOferente, puntosReceptor, ahora));
        // Los objetos ya se entregaron: sus listas de espera se cierran (en el mismo guardado, todo o nada).
        var avisos = new AvisosDiferidos();
        foreach (var entregada in ofrecidas.Prepend(pub))
        {
            foreach (var otra in await _solicitudes.ListarEnCursoPorPublicacionAsync(entregada.Id, incluirAceptada: false))
            {
                otra.Rechazar("El objeto ya se entregó a otra persona.");
                avisos.Agregar(otra.SolicitanteId, TiposNotificacion.SolicitudRechazada,
                    $"\"{entregada.Titulo}\" ya se entregó a otra persona. ¡Gracias por tu interés! Hay más publicaciones esperándote.", otra.Id);
            }
        }

        // Un solo SaveChanges = todo o nada. Con RowVersion, dos confirmaciones simultáneas no duplican puntos (la segunda recibe 409).
        await _uow.GuardarCambiosAsync();
        await avisos.EnviarAsync(_notificador);
        _log.LogInformation("Transacción completada {SolicitudId} modo {Modo}", s.Id, pub.Modo);
        foreach (var id in new[] { oferente.Id, receptor.Id })
            await NotificarAsync(id, TiposNotificacion.IntercambioCompletado, parejaRepetida
                ? $"¡Intercambio de \"{pub.Titulo}\" completado! Como ya intercambiaron hace poco, este no suma Eco-Puntos (se suman una vez cada {PoliticaEcoPuntos.DiasEntreTransaccionesConPuntosMismaPareja} días por pareja)."
                : $"¡Intercambio de \"{pub.Titulo}\" completado! Ya puedes calificar a la otra persona.", s.Id);
        foreach (var (invitado, invitador) in invitaciones)
        {
            await NotificarAsync(invitado, TiposNotificacion.InvitacionRecompensada,
                $"¡Completaste tu primer intercambio! Ganaste {PoliticaEcoPuntos.PuntosInvitacion} Eco-Puntos por llegar con una invitación.", s.Id);
            if (invitador is { } id)
                await _notificador.NotificarAsync(id, TiposNotificacion.InvitacionRecompensada,
                    $"Una persona que invitaste completó su primer intercambio: ganaste {PoliticaEcoPuntos.PuntosInvitacion} Eco-Puntos.");
        }
        await avisosPuntos.EnviarAsync(_notificador);
    }

    /// <summary>
    /// Invita y ganan los dos: el primer intercambio VÁLIDO de la persona invitada da puntos a ambos. No cuenta un intercambio con
    /// quien la invitó ni uno que el anti-farmeo dejó sin puntos (se espera al siguiente). Quien invita gana como máximo
    /// N invitaciones por ciclo; pasado el tope, la persona invitada recibe lo suyo igual.
    /// </summary>
    /// <returns>A quién avisar, o null si este intercambio no cierra la invitación.</returns>
    private async Task<(Guid Invitado, Guid? Invitador)?> RecompensarInvitacionAsync(Usuario invitado, Usuario otro, bool conPuntos, Guid solicitudId,
        DateTime ahora, AvisosEcoPuntos avisosPuntos)
    {
        if (!invitado.EsperaRecompensaInvitacion || !conPuntos || otro.Id == invitado.InvitadoPorId) return null;
        var invitador = await _usuarios.ObtenerPorIdAsync(invitado.InvitadoPorId!.Value);
        var invitadorGana = invitador is { EstaEliminado: false }
            && await _usuarios.ContarInvitacionesRecompensadasAsync(invitador.Id, ahora.AddDays(-PoliticaEcoPuntos.DiasCicloInvitaciones))
                < PoliticaEcoPuntos.MaxInvitacionesRecompensadasPorCiclo;
        invitado.RecibirRecompensaInvitacion(ahora, solicitudId);
        if (!invitadorGana) return (invitado.Id, null);
        avisosPuntos.Observar(invitador!);
        invitador!.AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosInvitacion, ConceptoEcoPuntos.Invitacion, null,
            "Una persona que invitaste completó su primer intercambio");
        return (invitado.Id, invitador.Id);
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
            var avisos = await LiberarPublicacionesAsync(s);
            await _uow.GuardarCambiosAsync();
            foreach (var id in new[] { s.SolicitanteId, s.Publicacion!.PropietarioId })
                await NotificarAsync(id, TiposNotificacion.IntercambioNoConcretado,
                    $"El intercambio de \"{s.Publicacion.Titulo}\" se cerró sin confirmación de entrega.", s.Id);
            await avisos.EnviarAsync(_notificador);
        }
    }

    public async Task<IReadOnlyList<Guid>> ListarParaRecordatorioAsync(int maximo)
    {
        var ahora = Ahora;
        var antes = Limites.AnticipacionRecordatorioCierre;
        return await _solicitudes.ListarParaRecordatorioAsync(ahora.AddDays(-Limites.DiasRecordatorioEntrega),
            ahora.AddDays(-Limites.DiasCierreConUnaConfirmacion).Add(antes), ahora.AddDays(-Limites.DiasCierreSinConfirmacion).Add(antes), maximo);
    }

    public async Task RecordarEntregaAsync(Guid solicitudId)
    {
        var s = await _solicitudes.ObtenerPorIdAsync(solicitudId);
        if (s is null || s.Estado != EstadoSolicitud.Aceptada || s.FechaAceptacionUtc is not { } aceptada) return;
        var pendientes = new[] { (s.Publicacion!.PropietarioId, s.ConfirmadaPorDuenioUtc), (s.SolicitanteId, s.ConfirmadaPorSolicitanteUtc) }
            .Where(p => p.Item2 is null).Select(p => p.Item1).ToList();
        if (pendientes.Count == 0) return;
        var ahora = Ahora;
        var titulo = s.Publicacion.Titulo;
        var unaConfirmo = pendientes.Count == 1;
        var cierre = aceptada.AddDays(unaConfirmo ? Limites.DiasCierreConUnaConfirmacion : Limites.DiasCierreSinConfirmacion);
        string mensaje;
        if (s.RecordatorioCierreUtc is null && cierre - ahora <= Limites.AnticipacionRecordatorioCierre)
        {
            s.MarcarRecordatorioCierre(ahora);
            mensaje = unaConfirmo
                ? $"Mañana se completa solo el intercambio de \"{titulo}\": la otra parte ya confirmó la entrega. Si no se concretó, márcalo hoy en Mis intercambios."
                : $"Mañana se cierra como no concretado el intercambio de \"{titulo}\" porque nadie confirmó la entrega. Si ya se hizo, confírmala hoy.";
        }
        else if (s.RecordatorioEntregaUtc is null && aceptada <= ahora.AddDays(-Limites.DiasRecordatorioEntrega))
        {
            s.MarcarRecordatorioEntrega(ahora);
            mensaje = $"¿Ya se hizo la entrega de \"{titulo}\"? Confírmala en Mis intercambios para cerrar el intercambio y sumar Eco-Puntos. Si no se concretó, también puedes indicarlo.";
        }
        else return;
        // Guardar antes de avisar: con RowVersion, dos procesos a la vez no envían el mismo recordatorio dos veces.
        await _uow.GuardarCambiosAsync();
        foreach (var id in pendientes) await NotificarAsync(id, TiposNotificacion.EntregaPendiente, mensaje, s.Id);
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
        var avisosPuntos = new AvisosEcoPuntos();
        var puntos = await PuntosPorCalificacionAsync(calificado, cuenta, r.Estrellas, s.Id, ahora);
        if (puntos > 0)
        {
            avisosPuntos.Observar(calificado);
            calificado.AcreditarEcoPuntos(puntos, ConceptoEcoPuntos.CalificacionRecibida, s.Id, s.Publicacion.Titulo);
        }
        // Índice único (solicitud, autor) y RowVersion de quien recibe: un doble envío simultáneo → 409 y los puntos se suman una vez.
        await _uow.GuardarCambiosAsync();
        await NotificarAsync(calificadoId, TiposNotificacion.CalificacionRecibida, puntos > 0
            ? $"Recibiste una calificación de {r.Estrellas} estrellas y ganaste {puntos} Eco-Puntos."
            : $"Recibiste una calificación de {r.Estrellas} estrella(s).", s.Id);
        await avisosPuntos.EnviarAsync(_notificador);
        return new CalificacionDto(c.Id, Mapeos.APerfilPublico(autor, ahora), c.Estrellas, c.Comentario, c.FechaUtc);
    }

    /// <summary>
    /// Eco-Puntos por una calificación de 4★ o 5★ (la llamada viene de un intercambio completado). Anti-farmeo: solo si la
    /// calificación cuenta (una por pareja en la ventana), con tope diario y una sola vez por intercambio.
    /// </summary>
    private async Task<int> PuntosPorCalificacionAsync(Usuario calificado, bool cuenta, int estrellas, Guid solicitudId, DateTime ahora)
    {
        var puntos = PoliticaEcoPuntos.PuntosPorCalificacion(estrellas);
        if (puntos == 0 || !cuenta || calificado.EstaEliminado || calificado.SuspensionVigente(ahora)) return 0;
        if (await _movimientos.ContarGanadosDesdeAsync(calificado.Id, ConceptoEcoPuntos.CalificacionRecibida, ahora.AddHours(-24))
            >= PoliticaEcoPuntos.MaxTransaccionesConPuntosPorDia) return 0;
        return await _movimientos.SumarAsync(calificado.Id, ConceptoEcoPuntos.CalificacionRecibida, solicitudId) > 0 ? 0 : puntos;
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

    private static SolicitudDto ADto(Solicitud s, Publicacion pub, Usuario solicitante, Guid actorId, Guid? conversacionId, bool puedoCalificar,
        DateTime ahora, IReadOnlyList<Publicacion> ofrecidas)
    {
        var resumenes = ofrecidas.Select(o => new PublicacionResumenDto(o.Id, o.Titulo,
            o.Imagenes.OrderBy(i => i.Orden).Select(i => i.Url).FirstOrDefault(), o.Condicion.ToString(), o.Estado.ToString())).ToList();
        var soyDuenio = pub.PropietarioId == actorId;
        DateTime? cierre = null;
        if (s.Estado == EstadoSolicitud.Aceptada && s.FechaAceptacionUtc is { } aceptada)
            cierre = aceptada.AddDays(s.ConfirmadaPorDuenioUtc is not null || s.ConfirmadaPorSolicitanteUtc is not null
                ? Limites.DiasCierreConUnaConfirmacion : Limites.DiasCierreSinConfirmacion);
        return new(s.Id, pub.Id, pub.Titulo, pub.Modo.ToString(), Mapeos.APerfilPublico(pub.Propietario!, ahora),
            Mapeos.APerfilPublico(solicitante, ahora), soyDuenio, s.FechaSolicitud, s.Mensaje, s.Estado.ToString(),
            s.MotivoRechazo, conversacionId, s.FechaAceptacionUtc, s.ConfirmadaPorDuenioUtc is not null, s.ConfirmadaPorSolicitanteUtc is not null,
            s.FechaCierreUtc, cierre, puedoCalificar,
            EnEspera: s.Estado == EstadoSolicitud.Pendiente && pub.Estado == EstadoPublicacionEnum.EnNegociacion, resumenes,
            pub.Modo == ModoTransaccion.Compra ? pub.PrecioReferenciaCop : null, s.PrecioPropuestoCop,
            MeTocaResponder: s.Estado == EstadoSolicitud.Pendiente && s.TurnoDuenio == soyDuenio, s.Contraofertas,
            Math.Max(0, Solicitud.MaxContraofertas - s.Contraofertas));
    }
}
