using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IChatService
{
    Task<IReadOnlyList<ConversacionDto>> ListarAsync(Guid actorId);
    /// <summary>Página de mensajes en orden cronológico (los más antiguos primero).</summary>
    Task<IReadOnlyList<MensajeChatDto>> ListarMensajesAsync(Guid actorId, Guid conversacionId, DateTime? antesDeUtc, int tamano);
    Task<MensajeChatDto> EnviarAsync(Guid actorId, Guid conversacionId, string texto, Guid? respuestaAId = null);
    Task MarcarLeidosAsync(Guid actorId, Guid conversacionId);
}

/// <summary>
/// Chat privado entre las dos partes de una solicitud. Quien no participa recibe 404 (ni siquiera sabe que existe).
/// Los mensajes se envían por HTTP (validación + persistencia) y SignalR solo los empuja a la contraparte.
/// </summary>
public sealed class ChatService : IChatService
{
    public const int MaxMensajesPorHora = 120;
    private const string TextoOculto = "Mensaje ocultado por moderación.";

    private readonly IConversacionRepository _conversaciones;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IEmisorTiempoReal _emisor;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ChatService> _log;

    public ChatService(IConversacionRepository conversaciones, IUsuarioRepository usuarios, IUnidadDeTrabajo uow,
        IEmisorTiempoReal emisor, TimeProvider reloj, ILogger<ChatService> log)
    {
        _conversaciones = conversaciones; _usuarios = usuarios; _uow = uow; _emisor = emisor; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public static bool EsEscribible(Conversacion c)
        => c.Solicitud?.Estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Aceptada or EstadoSolicitud.Completada
           && c.Duenio?.EstaEliminado == false && c.Solicitante?.EstaEliminado == false;

    private async Task<Conversacion> CargarAsync(Guid actorId, Guid conversacionId)
    {
        var c = await _conversaciones.ObtenerAsync(conversacionId);
        if (c is null || !c.EsParticipante(actorId)) throw new NoEncontradoException("Conversación no encontrada.");
        return c;
    }

    public const int LongitudCita = 150;

    public static MensajeChatDto ADto(Mensaje m, Guid lectorId)
        => new(m.Id, m.ConversacionId, m.AutorId == lectorId, m.EstaOculto ? TextoOculto : m.Texto, m.FechaUtc, m.LeidoUtc is not null, m.EstaOculto,
            m.RespuestaA is { } r
                ? new MensajeCitadoDto(r.Id, r.AutorId == lectorId,
                    r.EstaOculto ? TextoOculto : r.Texto.Length > LongitudCita ? r.Texto[..LongitudCita] + "…" : r.Texto, r.EstaOculto)
                : null);

    public async Task<IReadOnlyList<ConversacionDto>> ListarAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _conversaciones.ListarDeUsuarioAsync(actorId, 100);
        var ids = lista.Select(c => c.Id).ToList();
        var noLeidos = await _conversaciones.ContarNoLeidosAsync(actorId, ids);
        var ultimos = await _conversaciones.UltimosMensajesAsync(ids);

        return lista.Select(c =>
        {
            var soyDuenio = c.DuenioId == actorId;
            var otra = soyDuenio ? c.Solicitante! : c.Duenio!;
            string? vistaPrevia = null;
            if (ultimos.TryGetValue(c.Id, out var u))
                vistaPrevia = u.EstaOculto ? TextoOculto : u.Texto.Length > 100 ? u.Texto[..100] + "…" : u.Texto;
            return new ConversacionDto(c.Id, c.SolicitudId, c.PublicacionId, c.Solicitud!.Publicacion!.Titulo, c.Solicitud.Estado.ToString(),
                soyDuenio, Mapeos.APerfilPublico(otra, ahora), vistaPrevia, c.UltimoMensajeUtc,
                noLeidos.TryGetValue(c.Id, out var n) ? n : 0, EsEscribible(c));
        }).ToList();
    }

    public async Task<IReadOnlyList<MensajeChatDto>> ListarMensajesAsync(Guid actorId, Guid conversacionId, DateTime? antesDeUtc, int tamano)
    {
        await CargarAsync(actorId, conversacionId);
        var pagina = await _conversaciones.ListarMensajesAsync(conversacionId, antesDeUtc, tamano);
        return pagina.OrderBy(m => m.FechaUtc).Select(m => ADto(m, actorId)).ToList();
    }

    public async Task<MensajeChatDto> EnviarAsync(Guid actorId, Guid conversacionId, string texto, Guid? respuestaAId = null)
    {
        var c = await CargarAsync(actorId, conversacionId);
        var actor = actorId == c.DuenioId ? c.Duenio! : c.Solicitante!;
        Guardas.ExigirCorreoVerificado(actor);
        if (!EsEscribible(c)) throw new ReglaDeNegocioException("Esta conversación está cerrada: la solicitud fue rechazada, cancelada o no se concretó.");

        var ahora = Ahora;
        if (await _conversaciones.ContarMensajesDelAutorDesdeAsync(actorId, ahora.AddHours(-1)) >= MaxMensajesPorHora)
            throw new ReglaDeNegocioException("Estás enviando demasiados mensajes. Intenta de nuevo más tarde.");

        Mensaje? citado = null;
        if (respuestaAId is { } idCitado)
        {
            citado = await _conversaciones.ObtenerMensajeAsync(idCitado);
            if (citado is null || citado.ConversacionId != c.Id) throw new NoEncontradoException("El mensaje que quieres responder no existe.");
            if (citado.EstaOculto) throw new ReglaDeNegocioException("No puedes responder un mensaje ocultado por moderación.");
        }

        var m = new Mensaje(c.Id, actorId, texto, ahora, citado);
        _conversaciones.AgregarMensaje(m);
        c.RegistrarMensaje(ahora);
        await _uow.GuardarCambiosAsync();

        try
        {
            await _emisor.MensajeChatAsync(c.Contraparte(actorId), ADto(m, c.Contraparte(actorId)));
        }
        catch (Exception ex)
        {
            // El mensaje ya quedó guardado: la contraparte lo verá al abrir la conversación.
            _log.LogWarning(ex, "No se pudo empujar en tiempo real el mensaje {MensajeId}", m.Id);
        }
        return ADto(m, actorId);
    }

    public async Task MarcarLeidosAsync(Guid actorId, Guid conversacionId)
    {
        await CargarAsync(actorId, conversacionId);
        await _conversaciones.MarcarLeidosAsync(conversacionId, actorId, Ahora);
        await _uow.GuardarCambiosAsync();
    }
}
