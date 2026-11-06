using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Moderacion;

/// <summary>
/// Revisión del chat por moderadores. Nadie lee conversaciones al azar: solo los mensajes que el filtro marcó o que
/// alguien denunció, con los mensajes de alrededor para entender el contexto. Cada lectura queda en la auditoría.
/// </summary>
public interface IModeracionChatService
{
    Task<IReadOnlyList<MensajeMarcadoDto>> ListarMarcadosAsync(Guid actorId);
    /// <summary>404 si el mensaje no está marcado ni denunciado: así no sirve para leer cualquier chat.</summary>
    Task<ContextoChatDto> ContextoAsync(Guid actorId, Guid mensajeId);
    Task ResolverAsync(Guid actorId, Guid mensajeId, AccionMensajeMarcadoDto accion, string? nota);
}

public sealed class ModeracionChatService : IModeracionChatService
{
    public const int MensajesAlrededor = 10;
    private const int MaxFila = 100;
    private const int LongitudVistaPrevia = 300;
    private const string NotaPorDefecto = "Contenido no permitido en el chat";

    private readonly IConversacionRepository _conversaciones;
    private readonly IUsuarioRepository _usuarios;
    private readonly IDenunciaRepository _denuncias;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAlmacenArchivos _almacen;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ModeracionChatService> _log;

    public ModeracionChatService(IConversacionRepository conversaciones, IUsuarioRepository usuarios, IDenunciaRepository denuncias,
        IAuditoriaRepository auditoria, IUnidadDeTrabajo uow, IAlmacenArchivos almacen, INotificador notificador, TimeProvider reloj,
        ILogger<ModeracionChatService> log)
    {
        _conversaciones = conversaciones; _usuarios = usuarios; _denuncias = denuncias; _auditoria = auditoria; _uow = uow; _almacen = almacen;
        _notificador = notificador; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private async Task<Usuario> ExigirModeradorAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
        return actor;
    }

    public async Task<IReadOnlyList<MensajeMarcadoDto>> ListarMarcadosAsync(Guid actorId)
    {
        await ExigirModeradorAsync(actorId);
        var marcados = await _conversaciones.ListarMarcadosPendientesAsync(MaxFila);
        var nombres = new Dictionary<Guid, string>();
        foreach (var autor in marcados.Select(m => m.AutorId).Distinct())
            nombres[autor] = (await _usuarios.ObtenerPorIdAsync(autor))?.NombreCompleto ?? "Cuenta eliminada";
        return marcados.Select(m => new MensajeMarcadoDto(m.Id, m.ConversacionId, m.AutorId, nombres[m.AutorId],
                m.Texto.Length > LongitudVistaPrevia ? m.Texto[..LongitudVistaPrevia] + "…" : m.Texto, m.FotoUrl is not null,
                m.MotivoRevision ?? "", m.MarcadoUtc!.Value))
            .ToList();
    }

    public async Task<ContextoChatDto> ContextoAsync(Guid actorId, Guid mensajeId)
    {
        await ExigirModeradorAsync(actorId);
        var m = await _conversaciones.ObtenerMensajeAsync(mensajeId) ?? throw new NoEncontradoException("Mensaje no encontrado.");
        var denuncias = await _denuncias.ContarSobreObjetivoAsync(TipoObjetoDenuncia.Mensaje, mensajeId);
        if (m.MarcadoUtc is null && denuncias == 0) throw new NoEncontradoException("Mensaje no encontrado.");

        var c = await _conversaciones.ObtenerAsync(m.ConversacionId) ?? throw new NoEncontradoException("Mensaje no encontrado.");
        var mensajes = await _conversaciones.ContextoAsync(m, MensajesAlrededor);
        var fotos = mensajes.Where(x => x.FotoUrl is not null).Select(x => x.FotoUrl!).Distinct(StringComparer.Ordinal).ToList();
        var firmadas = fotos.Count == 0 ? new Dictionary<string, string>() : await _almacen.UrlsLecturaChatAsync(fotos);

        _auditoria.Agregar(new AuditoriaEvento(actorId, "CHAT_CONTEXTO_VISTO", "Mensaje", mensajeId,
            $"Conversación {c.Id}; {mensajes.Count} mensajes", Ahora));
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Moderador {ActorId} leyó el contexto del mensaje {MensajeId}", actorId, mensajeId);

        return new ContextoChatDto(m.Id, c.Id, c.Solicitud?.Publicacion?.Titulo,
            [new(c.DuenioId, c.Duenio?.NombreCompleto ?? "Cuenta eliminada"), new(c.SolicitanteId, c.Solicitante?.NombreCompleto ?? "Cuenta eliminada")],
            mensajes.Select(x => new MensajeContextoDto(x.Id, x.AutorId, x.Texto,
                    x.FotoUrl is { } f && firmadas.TryGetValue(f, out var firmada) ? firmada : null,
                    x.FechaUtc, x.EstaOculto, x.MotivoRevision, x.Id == m.Id))
                .ToList(),
            m.MarcaPendiente, denuncias);
    }

    public async Task ResolverAsync(Guid actorId, Guid mensajeId, AccionMensajeMarcadoDto accion, string? nota)
    {
        await ExigirModeradorAsync(actorId);
        var m = await _conversaciones.ObtenerMensajeAsync(mensajeId) ?? throw new NoEncontradoException("Mensaje no encontrado.");
        if (m.MarcadoUtc is null) throw new NoEncontradoException("Mensaje no encontrado.");
        if (m.Conversacion!.EsParticipante(actorId)) throw new ReglaDeNegocioException("No puedes revisar mensajes de tus propias conversaciones.");
        if (!m.MarcaPendiente) throw new ConflictoDeConcurrenciaException("Otro moderador ya revisó este mensaje.");

        var ahora = Ahora;
        m.RegistrarRevisionMarca(ahora);
        var motivo = string.IsNullOrWhiteSpace(nota) ? NotaPorDefecto : nota.Trim();
        if (accion == AccionMensajeMarcadoDto.Ocultar) m.Ocultar(motivo);
        _auditoria.Agregar(new AuditoriaEvento(actorId, accion == AccionMensajeMarcadoDto.Ocultar ? "CHAT_MENSAJE_OCULTADO" : "CHAT_MARCA_DESCARTADA",
            "Mensaje", mensajeId, accion == AccionMensajeMarcadoDto.Ocultar ? motivo : null, ahora));
        await _uow.GuardarCambiosAsync();

        if (accion == AccionMensajeMarcadoDto.Ocultar)
            await _notificador.NotificarAsync(m.AutorId, TiposNotificacion.MensajeOcultado,
                $"Ocultamos un mensaje tuyo en el chat porque no cumple las normas de la comunidad: {motivo}.", m.ConversacionId);
    }
}
