using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Presentacion.TiempoReal;

/// <summary>
/// Canal en tiempo real. Cada usuario recibe ÚNICAMENTE lo suyo (Clients.User por claim "sub").
/// Enviar mensajes de chat se hace por HTTP (POST /conversaciones/{id}/mensajes), donde se valida y persiste.
/// Desde el navegador solo se pueden invocar dos avisos livianos, ambos validados en Negocio:
/// <see cref="Escribiendo"/> ("escribiendo…", máximo uno por segundo) y <see cref="Recibido"/> (✓✓ entregado).
/// </summary>
[Authorize]
public sealed class NotificacionesHub : Hub
{
    public const string Ruta = "/hubs/notificaciones";
    public const string EventoNotificacion = "notificacion";
    public const string EventoMensaje = "mensaje";
    public const string EventoEstadoMensajes = "estadoMensajes";
    public const string EventoEscribiendo = "escribiendo";
    public const string EventoPresencia = "presencia";

    private readonly IChatService _chat;
    private readonly IPresencia _presencia;
    private readonly IEmisorTiempoReal _emisor;
    private readonly ILogger<NotificacionesHub> _log;

    public NotificacionesHub(IChatService chat, IPresencia presencia, IEmisorTiempoReal emisor, ILogger<NotificacionesHub> log)
    {
        _chat = chat; _presencia = presencia; _emisor = emisor; _log = log;
    }

    private Guid? UsuarioId => Guid.TryParse(Context.UserIdentifier, out var id) ? id : null;

    public override async Task OnConnectedAsync()
    {
        if (UsuarioId is { } id)
        {
            try
            {
                if (await _presencia.ConectarAsync(id, Context.ConnectionId)) await AvisarPresenciaAsync(id, true);
                // Lo que le escribieron mientras no estaba ya llegó a su dispositivo: ✓✓ para los autores.
                await _chat.MarcarEntregadosAsync(id);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Falló la presencia o la entrega al conectar {UsuarioId}", id);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (UsuarioId is { } id)
        {
            try
            {
                if (await _presencia.DesconectarAsync(id, Context.ConnectionId)) await AvisarPresenciaAsync(id, false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Falló la presencia al desconectar {UsuarioId}", id);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>El usuario está escribiendo en la conversación: se avisa solo a la contraparte.</summary>
    public async Task Escribiendo(Guid conversacionId)
    {
        if (UsuarioId is not { } id) return;
        // Máximo un aviso por segundo por conexión (el cliente ya envía cada ~3 s)
        var ahora = Environment.TickCount64;
        if (Context.Items.TryGetValue("ultimoEscribiendo", out var previo) && previo is long p && ahora - p < 1000) return;
        Context.Items["ultimoEscribiendo"] = ahora;

        // La contraparte se recuerda por conexión: no se consulta la base en cada tecla.
        var cache = Context.Items.TryGetValue("contrapartes", out var c) && c is Dictionary<Guid, Guid?> d ? d : null;
        if (cache is null) Context.Items["contrapartes"] = cache = new Dictionary<Guid, Guid?>();
        if (!cache.TryGetValue(conversacionId, out var otra))
        {
            if (cache.Count > 50) cache.Clear();
            cache[conversacionId] = otra = await _chat.ContraparteParaEscribirAsync(id, conversacionId);
        }
        if (otra is { } destino) await _emisor.EscribiendoAsync(destino, new EscribiendoDto(conversacionId));
    }

    /// <summary>El navegador recibió mensajes de esta conversación (✓✓ entregado).</summary>
    public Task Recibido(Guid conversacionId)
        => UsuarioId is { } id ? _chat.MarcarEntregadosAsync(id, conversacionId) : Task.CompletedTask;

    private async Task AvisarPresenciaAsync(Guid usuarioId, bool enLinea)
    {
        var contrapartes = await _chat.ContrapartesAsync(usuarioId);
        if (contrapartes.Count > 0) await _emisor.PresenciaAsync(contrapartes, new PresenciaDto(usuarioId, enLinea));
    }
}

/// <summary>Identifica la conexión con el claim "sub" del JWT (no con el nombre ni el correo).</summary>
public sealed class UsuarioIdPorSub : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

/// <summary>Canal en tiempo real de Negocio. Con Redis habilitado, llega a usuarios conectados a cualquier instancia.</summary>
public sealed class EmisorSignalR : IEmisorTiempoReal
{
    private readonly IHubContext<NotificacionesHub> _hub;
    public EmisorSignalR(IHubContext<NotificacionesHub> hub) => _hub = hub;

    private IClientProxy A(Guid usuarioId) => _hub.Clients.User(usuarioId.ToString());

    public Task NotificacionAsync(Guid usuarioId, NotificacionDto notificacion)
        => A(usuarioId).SendAsync(NotificacionesHub.EventoNotificacion, notificacion);

    public Task MensajeChatAsync(Guid usuarioId, MensajeChatDto mensaje)
        => A(usuarioId).SendAsync(NotificacionesHub.EventoMensaje, mensaje);

    public Task EstadoMensajesAsync(Guid usuarioId, EstadoMensajesDto estado)
        => A(usuarioId).SendAsync(NotificacionesHub.EventoEstadoMensajes, estado);

    public Task EscribiendoAsync(Guid usuarioId, EscribiendoDto escribiendo)
        => A(usuarioId).SendAsync(NotificacionesHub.EventoEscribiendo, escribiendo);

    public Task PresenciaAsync(IReadOnlyCollection<Guid> usuarioIds, PresenciaDto presencia)
        => _hub.Clients.Users(usuarioIds.Select(u => u.ToString()).ToList()).SendAsync(NotificacionesHub.EventoPresencia, presencia);
}

/// <summary>
/// Presencia compartida entre instancias (Redis): un conjunto ordenado por usuario con sus conexiones y la hora de conexión.
/// Las conexiones viven como mucho lo que dura el JWT (el servidor las cierra al vencer), así que las de más de 30 min
/// se consideran restos de una instancia caída y se descartan: nadie queda "en línea" para siempre.
/// </summary>
public sealed class PresenciaRedis : IPresencia
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);
    private readonly IConnectionMultiplexer _redis;
    public PresenciaRedis(IConnectionMultiplexer redis) => _redis = redis;

    private static RedisKey Clave(Guid usuarioId) => $"trueke:presencia:{usuarioId:N}";
    private static double Ahora => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static async Task<long> VigentesAsync(IDatabase db, RedisKey clave)
    {
        await db.SortedSetRemoveRangeByScoreAsync(clave, double.NegativeInfinity, Ahora - Vigencia.TotalSeconds);
        return await db.SortedSetLengthAsync(clave);
    }

    public async Task<bool> ConectarAsync(Guid usuarioId, string conexionId)
    {
        var db = _redis.GetDatabase();
        var clave = Clave(usuarioId);
        await db.SortedSetAddAsync(clave, conexionId, Ahora);
        await db.KeyExpireAsync(clave, Vigencia);
        return await VigentesAsync(db, clave) == 1;
    }

    public async Task<bool> DesconectarAsync(Guid usuarioId, string conexionId)
    {
        var db = _redis.GetDatabase();
        var clave = Clave(usuarioId);
        return await db.SortedSetRemoveAsync(clave, conexionId) && await VigentesAsync(db, clave) == 0;
    }

    public async Task<IReadOnlySet<Guid>> EnLineaAsync(IReadOnlyCollection<Guid> usuarioIds)
    {
        var db = _redis.GetDatabase();
        var resultado = new HashSet<Guid>();
        foreach (var id in usuarioIds)
            if (await VigentesAsync(db, Clave(id)) > 0) resultado.Add(id);
        return resultado;
    }
}
