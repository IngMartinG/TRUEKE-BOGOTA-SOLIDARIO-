using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Push;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IChatService
{
    Task<IReadOnlyList<ConversacionDto>> ListarAsync(Guid actorId);
    /// <summary>Página de mensajes en orden cronológico (los más antiguos primero).</summary>
    Task<IReadOnlyList<MensajeChatDto>> ListarMensajesAsync(Guid actorId, Guid conversacionId, DateTime? antesDeUtc, int tamano);
    /// <summary>Con <paramref name="fotoUrl"/> (subida tipo FotoChat) el texto es opcional: la foto se limpia y queda privada.</summary>
    Task<MensajeChatDto> EnviarAsync(Guid actorId, Guid conversacionId, string texto, Guid? respuestaAId = null, string? fotoUrl = null);
    Task MarcarLeidosAsync(Guid actorId, Guid conversacionId);
    /// <summary>Los mensajes recibidos llegaron al dispositivo (✓✓). Sin conversación: todos los pendientes del usuario.</summary>
    Task MarcarEntregadosAsync(Guid actorId, Guid? conversacionId = null);
    /// <summary>La otra parte, si el actor participa y aún pueden escribirse; si no, null (para "escribiendo…").</summary>
    Task<Guid?> ContraparteParaEscribirAsync(Guid actorId, Guid conversacionId);
    /// <summary>Personas con las que el usuario tiene conversaciones (a quienes se avisa si entra o sale de línea).</summary>
    Task<IReadOnlyList<Guid>> ContrapartesAsync(Guid actorId);
}

/// <summary>
/// Chat privado entre las dos partes de una solicitud. Quien no participa recibe 404 (ni siquiera sabe que existe).
/// Los mensajes se envían por HTTP (validación + persistencia) y SignalR solo los empuja a la contraparte.
/// </summary>
public sealed class ChatService : IChatService
{
    public const int MaxMensajesPorHora = 120;
    /// <summary>Fotos por persona al día (en todas sus conversaciones): suficiente para mostrar un objeto, no para llenar el almacenamiento.</summary>
    public const int MaxFotosPorDia = 20;
    private const string TextoOculto = "Mensaje ocultado por moderación.";
    private const string TextoFoto = "📷 Foto";

    private readonly IConversacionRepository _conversaciones;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IEmisorTiempoReal _emisor;
    private readonly IPresencia _presencia;
    private readonly IBloqueoRepository _bloqueos;
    private readonly IAvisosCorreo _avisosCorreo;
    private readonly IAvisosPush _avisosPush;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ChatService> _log;
    private readonly IAlmacenArchivos _almacen;
    private readonly UrlsOpciones _urls;

    public ChatService(IConversacionRepository conversaciones, IUsuarioRepository usuarios, IUnidadDeTrabajo uow,
        IEmisorTiempoReal emisor, IPresencia presencia, IBloqueoRepository bloqueos, IAvisosCorreo avisosCorreo, IAvisosPush avisosPush, TimeProvider reloj,
        ILogger<ChatService> log, IAlmacenArchivos almacen, IOptions<UrlsOpciones> urls)
    {
        _conversaciones = conversaciones; _usuarios = usuarios; _uow = uow; _emisor = emisor; _presencia = presencia; _bloqueos = bloqueos;
        _avisosCorreo = avisosCorreo; _avisosPush = avisosPush; _reloj = reloj; _log = log; _almacen = almacen; _urls = urls.Value;
    }

    /// <summary>El tiempo real es un extra: si falla, la operación ya quedó guardada y se verá al recargar.</summary>
    private async Task EmitirAsync(Func<Task> emitir, string que)
    {
        try { await emitir(); }
        catch (Exception ex) { _log.LogWarning(ex, "No se pudo emitir en tiempo real: {Que}", que); }
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

    /// <param name="fotosFirmadas">URL guardada → URL firmada para leerla. Un mensaje oculto nunca muestra su foto.</param>
    public static MensajeChatDto ADto(Mensaje m, Guid lectorId, IReadOnlyDictionary<string, string>? fotosFirmadas = null)
        => new(m.Id, m.ConversacionId, m.AutorId == lectorId, m.EstaOculto ? TextoOculto : m.Texto, m.FechaUtc, m.LeidoUtc is not null, m.EstaOculto,
            m.RespuestaA is { } r
                ? new MensajeCitadoDto(r.Id, r.AutorId == lectorId, r.EstaOculto ? TextoOculto : Resumen(r, LongitudCita), r.EstaOculto)
                : null,
            m.LeidoUtc is not null ? EstadoMensajeDto.Leido : m.EntregadoUtc is not null ? EstadoMensajeDto.Entregado : EstadoMensajeDto.Enviado,
            TieneFoto: m.FotoUrl is not null && !m.EstaOculto,
            FotoUrl: !m.EstaOculto && m.FotoUrl is { } f && fotosFirmadas?.TryGetValue(f, out var firmada) == true ? firmada : null);

    /// <summary>Texto recortado para citas y la bandeja; una foto sin pie se resume como "📷 Foto".</summary>
    private static string Resumen(Mensaje m, int largo)
    {
        if (m.Texto.Length == 0) return m.FotoUrl is not null ? TextoFoto : "";
        var texto = m.Texto.Length > largo ? m.Texto[..largo] + "…" : m.Texto;
        return m.FotoUrl is not null ? $"{TextoFoto}: {texto}" : texto;
    }

    private Task<IReadOnlyDictionary<string, string>> FirmarFotosAsync(IEnumerable<Mensaje> mensajes)
    {
        var urls = mensajes.Where(m => !m.EstaOculto && m.FotoUrl is not null).Select(m => m.FotoUrl!).Distinct(StringComparer.Ordinal).ToList();
        return urls.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>())
            : _almacen.UrlsLecturaChatAsync(urls);
    }

    public async Task<IReadOnlyList<ConversacionDto>> ListarAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _conversaciones.ListarDeUsuarioAsync(actorId, 100);
        var ids = lista.Select(c => c.Id).ToList();
        var noLeidos = await _conversaciones.ContarNoLeidosAsync(actorId, ids);
        var ultimos = await _conversaciones.UltimosMensajesAsync(ids);
        var enLinea = await _presencia.EnLineaAsync(lista.Select(c => c.Contraparte(actorId)).Distinct().ToList());
        // Con un bloqueo (en cualquier dirección) la conversación queda de solo lectura y no se ve "en línea".
        var bloqueados = await _bloqueos.RelacionadosAsync(actorId);
        var yoBloquee = bloqueados.Count == 0 ? new HashSet<Guid>() : (await _bloqueos.ListarDeAsync(actorId)).Select(b => b.BloqueadoId).ToHashSet();

        return lista.Select(c =>
        {
            var soyDuenio = c.DuenioId == actorId;
            var otra = soyDuenio ? c.Solicitante! : c.Duenio!;
            string? vistaPrevia = null;
            if (ultimos.TryGetValue(c.Id, out var u))
                vistaPrevia = u.EstaOculto ? TextoOculto : Resumen(u, 100);
            return new ConversacionDto(c.Id, c.SolicitudId, c.PublicacionId, c.Solicitud!.Publicacion!.Titulo, c.Solicitud.Estado.ToString(),
                soyDuenio, Mapeos.APerfilPublico(otra, ahora), vistaPrevia, c.UltimoMensajeUtc,
                noLeidos.TryGetValue(c.Id, out var n) ? n : 0, EsEscribible(c) && !bloqueados.Contains(otra.Id),
                enLinea.Contains(otra.Id) && !otra.EstaEliminado && !bloqueados.Contains(otra.Id), yoBloquee.Contains(otra.Id));
        }).ToList();
    }

    public async Task<IReadOnlyList<MensajeChatDto>> ListarMensajesAsync(Guid actorId, Guid conversacionId, DateTime? antesDeUtc, int tamano)
    {
        await CargarAsync(actorId, conversacionId);
        var pagina = await _conversaciones.ListarMensajesAsync(conversacionId, antesDeUtc, tamano);
        var fotos = await FirmarFotosAsync(pagina);
        return pagina.OrderBy(m => m.FechaUtc).Select(m => ADto(m, actorId, fotos)).ToList();
    }

    public async Task<MensajeChatDto> EnviarAsync(Guid actorId, Guid conversacionId, string texto, Guid? respuestaAId = null, string? fotoUrl = null)
    {
        var c = await CargarAsync(actorId, conversacionId);
        var actor = actorId == c.DuenioId ? c.Duenio! : c.Solicitante!;
        Guardas.ExigirCuentaCompleta(actor);
        if (!EsEscribible(c)) throw new ReglaDeNegocioException("Esta conversación está cerrada: la solicitud fue rechazada, cancelada o no se concretó.");
        if (await _bloqueos.ExisteEntreAsync(actorId, c.Contraparte(actorId)))
            throw new ReglaDeNegocioException("No puedes enviar mensajes en esta conversación.");

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

        fotoUrl = string.IsNullOrWhiteSpace(fotoUrl) ? null : fotoUrl.Trim();
        if (fotoUrl is null && string.IsNullOrWhiteSpace(texto))
            throw new ReglaDeNegocioException($"El mensaje debe tener entre 1 y {Mensaje.LongitudMaxima} caracteres.");
        if (fotoUrl is not null)
        {
            if (await _conversaciones.ContarFotosDelAutorDesdeAsync(actorId, ahora.AddDays(-1)) >= MaxFotosPorDia)
                throw new ReglaDeNegocioException($"Puedes enviar como máximo {MaxFotosPorDia} fotos al día. Intenta de nuevo mañana.");
            // Lo último antes de guardar: valida, limpia (sin GPS ni metadatos) y copia la foto al bucket privado del chat
            if (_almacen.Habilitado) fotoUrl = await _almacen.ValidarArchivoPropioAsync(fotoUrl, actorId, TipoArchivoDto.FotoChat);
            else ValidadorUrls.ExigirHostPermitido(fotoUrl, _urls.HostsPermitidosImagenes, "La foto");
        }

        var m = new Mensaje(c.Id, actorId, texto, ahora, citado, fotoUrl);
        _conversaciones.AgregarMensaje(m);
        c.RegistrarMensaje(ahora);
        await _uow.GuardarCambiosAsync();

        var fotos = await FirmarFotosAsync([m]);
        try
        {
            await _emisor.MensajeChatAsync(c.Contraparte(actorId), ADto(m, c.Contraparte(actorId), fotos));
        }
        catch (Exception ex)
        {
            // El mensaje ya quedó guardado: la contraparte lo verá al abrir la conversación.
            _log.LogWarning(ex, "No se pudo empujar en tiempo real el mensaje {MensajeId}", m.Id);
        }
        // Si la otra persona no tiene la app abierta: un correo (sin el texto) como mucho cada 2 horas por conversación y un aviso push
        await _avisosCorreo.MensajeChatAsync(c.Contraparte(actorId), c.Id, Mapeos.NombrePublico(actor.NombreCompleto));
        await _avisosPush.MensajeChatAsync(c.Contraparte(actorId), c.Id, Mapeos.NombrePublico(actor.NombreCompleto));
        return ADto(m, actorId, fotos);
    }

    public async Task MarcarLeidosAsync(Guid actorId, Guid conversacionId)
    {
        var c = await CargarAsync(actorId, conversacionId);
        var hasta = await _conversaciones.MarcarLeidosAsync(conversacionId, actorId, Ahora);
        await _uow.GuardarCambiosAsync();
        if (hasta is { } h)
            await EmitirAsync(() => _emisor.EstadoMensajesAsync(c.Contraparte(actorId), new EstadoMensajesDto(conversacionId, h, h)), "leídos");
    }

    public async Task MarcarEntregadosAsync(Guid actorId, Guid? conversacionId = null)
    {
        // El repositorio solo toca conversaciones del actor: no hace falta validar la participación aparte.
        var marcados = await _conversaciones.MarcarEntregadosAsync(actorId, conversacionId, Ahora);
        if (marcados.Count == 0) return;
        await _uow.GuardarCambiosAsync();
        foreach (var (conv, autor, hasta) in marcados)
            await EmitirAsync(() => _emisor.EstadoMensajesAsync(autor, new EstadoMensajesDto(conv, hasta, null)), "entregados");
    }

    public async Task<Guid?> ContraparteParaEscribirAsync(Guid actorId, Guid conversacionId)
    {
        var c = await _conversaciones.ObtenerAsync(conversacionId);
        if (c is null || !c.EsParticipante(actorId) || !EsEscribible(c)) return null;
        var otra = c.Contraparte(actorId);
        return await _bloqueos.ExisteEntreAsync(actorId, otra) ? null : otra;
    }

    public async Task<IReadOnlyList<Guid>> ContrapartesAsync(Guid actorId)
    {
        var bloqueados = await _bloqueos.RelacionadosAsync(actorId);
        return (await _conversaciones.ListarDeUsuarioAsync(actorId, 100)).Select(c => c.Contraparte(actorId))
            .Where(id => !bloqueados.Contains(id)).Distinct().ToList();
    }
}
