using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Channels;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio.Push;

/// <summary>
/// Notificaciones push (estándar Web Push con llaves VAPID). Sin llaves configuradas, la función queda apagada y el
/// front no ofrece activarla. Generar las llaves: docs/DESPLIEGUE.md §6.2.
/// </summary>
public sealed class PushOpciones
{
    public const string Seccion = "Push";
    /// <summary>Llave pública VAPID (P-256 sin comprimir, base64url, 65 bytes). Es pública: el navegador la necesita.</summary>
    public string ClavePublica { get; set; } = "";
    /// <summary>Llave privada VAPID (base64url, 32 bytes). SECRETA: solo por variable de entorno.</summary>
    public string ClavePrivada { get; set; } = "";
    /// <summary>Contacto para los servicios de push ("mailto:..." o https). Vacío = Legal:CorreoContacto.</summary>
    public string Asunto { get; set; } = "";

    public bool Habilitado => !string.IsNullOrWhiteSpace(ClavePublica) && !string.IsNullOrWhiteSpace(ClavePrivada);

    public bool Validas => !Habilitado || (Largo(ClavePublica) == 65 && Largo(ClavePrivada) == 32);

    private static int Largo(string base64Url)
    {
        try { return Convert.FromBase64String(base64Url.Replace('-', '+').Replace('_', '/').PadRight((base64Url.Length + 3) / 4 * 4, '=')).Length; }
        catch (FormatException) { return -1; }
    }
}

public sealed class SuscripcionPushRequest
{
    [Required, StringLength(1000, MinimumLength = 20)] public string Endpoint { get; init; } = "";
    [Required, StringLength(200, MinimumLength = 20)] public string P256dh { get; init; } = "";
    [Required, StringLength(100, MinimumLength = 8)] public string Auth { get; init; } = "";
}

public sealed class BajaPushRequest
{
    [Required, StringLength(1000)] public string Endpoint { get; init; } = "";
}

/// <summary>Reglas puras (sin red): las prueban las pruebas unitarias.</summary>
public static class ReglasPush
{
    /// <summary>
    /// Servicios de push de los navegadores. El servidor hace POST al Endpoint que manda el navegador: si se aceptara
    /// cualquier URL, alguien podría usar la API para hacer peticiones a otros sitios o a la red interna (SSRF).
    /// </summary>
    private static readonly string[] Servicios =
    [
        "fcm.googleapis.com",          // Chrome, Edge (Android), Opera, Samsung
        "push.services.mozilla.com",   // Firefox (updates.push.services.mozilla.com)
        "notify.windows.com",          // Edge en Windows (wns2-*.notify.windows.com)
        "push.apple.com",              // Safari / iPhone (web.push.apple.com)
    ];

    public static bool EndpointPermitido(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort
            || !string.IsNullOrEmpty(u.UserInfo)) return false;
        var host = u.IdnHost.ToLowerInvariant();
        return Servicios.Any(s => host == s || host.EndsWith("." + s, StringComparison.Ordinal));
    }

    /// <summary>Formato del mensaje que entiende el service worker de Angular (ngsw): muestra el aviso y, al tocarlo, abre la ruta.</summary>
    public static string Carga(string titulo, string cuerpo, string ruta, string etiqueta) => JsonSerializer.Serialize(new
    {
        notification = new
        {
            title = titulo,
            body = cuerpo,
            icon = "icono-192.png",
            badge = "favicon-32.png",
            tag = etiqueta,
            renotify = true,
            lang = "es-CO",
            // Ruta RELATIVA: el service worker la resuelve contra su alcance (sirve con subcarpeta o dominio propio).
            data = new { onActionClick = new { @default = new { operation = "navigateLastFocusedOrOpen", url = ruta } } }
        }
    });
}

public interface IPushService
{
    Task RegistrarAsync(Guid actorId, SuscripcionPushRequest r);
    Task QuitarAsync(Guid actorId, string endpoint);
    /// <summary>Al cerrar todas las sesiones o eliminar la cuenta: ningún dispositivo vuelve a recibir avisos.</summary>
    Task QuitarTodasAsync(Guid usuarioId);
}

public sealed class PushService : IPushService
{
    private readonly ISuscripcionPushRepository _repo;
    private readonly IUnidadDeTrabajo _uow;
    private readonly PushOpciones _o;
    private readonly TimeProvider _reloj;

    public PushService(ISuscripcionPushRepository repo, IUnidadDeTrabajo uow, IOptions<PushOpciones> o, TimeProvider reloj)
    {
        _repo = repo; _uow = uow; _o = o.Value; _reloj = reloj;
    }

    public async Task RegistrarAsync(Guid actorId, SuscripcionPushRequest r)
    {
        if (!_o.Habilitado) throw new ReglaDeNegocioException("Las notificaciones push no están disponibles en este momento.");
        if (!ReglasPush.EndpointPermitido(r.Endpoint)) throw new ReglaDeNegocioException("Este navegador no es compatible con las notificaciones de Trueke.");
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        var existente = await _repo.PorEndpointAsync(r.Endpoint);
        if (existente is not null) existente.Renovar(actorId, r.P256dh, r.Auth, ahora); // también si el navegador cambió de cuenta
        else
        {
            // Tope de dispositivos: se retira el más antiguo (un navegador viejo que ya no se usa).
            var actuales = await _repo.DeUsuarioAsync(actorId);
            foreach (var vieja in actuales.Skip(SuscripcionPush.MaxPorUsuario - 1)) _repo.Quitar(vieja);
            _repo.Agregar(new SuscripcionPush(actorId, r.Endpoint, r.P256dh, r.Auth, ahora));
        }
        await _uow.GuardarCambiosAsync();
    }

    public async Task QuitarAsync(Guid actorId, string endpoint)
    {
        var s = await _repo.PorEndpointAsync(endpoint);
        if (s is null || s.UsuarioId != actorId) return; // idempotente y sin revelar suscripciones ajenas
        _repo.Quitar(s);
        await _uow.GuardarCambiosAsync();
    }

    public async Task QuitarTodasAsync(Guid usuarioId)
    {
        var todas = await _repo.DeUsuarioAsync(usuarioId);
        if (todas.Count == 0) return;
        foreach (var s in todas) _repo.Quitar(s);
        await _uow.GuardarCambiosAsync();
    }
}

/// <summary>Un aviso para TODOS los dispositivos de una persona.</summary>
public sealed record AvisoPush(Guid UsuarioId, string Titulo, string Cuerpo, string Ruta, string Etiqueta);

/// <summary>Cola en memoria: quien notifica no espera a los servicios de push (como con el correo).</summary>
public sealed class ColaPush
{
    private readonly Channel<AvisoPush> _canal = Channel.CreateBounded<AvisoPush>(
        new BoundedChannelOptions(2000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
    private readonly ILogger<ColaPush> _log;
    public ColaPush(ILogger<ColaPush> log) => _log = log;

    public ChannelReader<AvisoPush> Lector => _canal.Reader;

    public void Encolar(AvisoPush aviso)
    {
        if (!_canal.Writer.TryWrite(aviso)) _log.LogWarning("Cola de push llena: se descartó un aviso");
    }
}

/// <summary>Entrega lo encolado a cada dispositivo. Las suscripciones que el servicio da por vencidas (404/410) se borran.</summary>
public sealed class EnvioPushHostedService : BackgroundService
{
    public const string ClienteHttp = "push";

    private readonly ColaPush _cola;
    private readonly IServiceScopeFactory _fabrica;
    private readonly IHttpClientFactory _http;
    private readonly PushOpciones _o;
    private readonly string _asunto;
    private readonly ILogger<EnvioPushHostedService> _log;

    public EnvioPushHostedService(ColaPush cola, IServiceScopeFactory fabrica, IHttpClientFactory http, IOptions<PushOpciones> o,
        IOptions<LegalOpciones> legal, ILogger<EnvioPushHostedService> log)
    {
        _cola = cola; _fabrica = fabrica; _http = http; _o = o.Value; _log = log;
        _asunto = !string.IsNullOrWhiteSpace(_o.Asunto) ? _o.Asunto
            : !string.IsNullOrWhiteSpace(legal.Value.CorreoContacto) ? $"mailto:{legal.Value.CorreoContacto}" : "mailto:soporte@trueke.co";
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_o.Habilitado) return;
        var cliente = new PushServiceClient(_http.CreateClient(ClienteHttp))
        {
            DefaultAuthentication = new VapidAuthentication(_o.ClavePublica, _o.ClavePrivada) { Subject = _asunto },
            DefaultTimeToLive = 24 * 60 * 60, // si el dispositivo está apagado, el aviso espera hasta un día
        };

        await foreach (var aviso in _cola.Lector.ReadAllAsync(ct))
        {
            try { await EntregarAsync(cliente, aviso, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "No se pudo entregar un aviso push al usuario {UsuarioId}", aviso.UsuarioId);
            }
        }
    }

    private async Task EntregarAsync(PushServiceClient cliente, AvisoPush aviso, CancellationToken ct)
    {
        using var scope = _fabrica.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ISuscripcionPushRepository>();
        var suscripciones = await repo.DeUsuarioAsync(aviso.UsuarioId);
        if (suscripciones.Count == 0) return;

        var mensaje = new PushMessage(ReglasPush.Carga(aviso.Titulo, aviso.Cuerpo, aviso.Ruta, aviso.Etiqueta)) { Urgency = PushMessageUrgency.High };
        var vencidas = new List<Guid>();
        foreach (var s in suscripciones)
        {
            if (!ReglasPush.EndpointPermitido(s.Endpoint)) { vencidas.Add(s.Id); continue; }
            var destino = new PushSubscription { Endpoint = s.Endpoint };
            destino.SetKey(PushEncryptionKeyName.P256DH, s.P256dh);
            destino.SetKey(PushEncryptionKeyName.Auth, s.Auth);
            try
            {
                await cliente.RequestPushMessageDeliveryAsync(destino, mensaje, ct);
            }
            catch (PushServiceClientException ex) when ((int)ex.StatusCode is 404 or 410)
            {
                vencidas.Add(s.Id); // el navegador retiró el permiso o se desinstaló la app
            }
            catch (PushServiceClientException ex)
            {
                _log.LogWarning("El servicio de push respondió {Estado} para un dispositivo del usuario {UsuarioId}", (int)ex.StatusCode, aviso.UsuarioId);
            }
        }
        await repo.EliminarAsync(vencidas);
    }
}

/// <summary>
/// Avisos push con las mismas reglas que los del correo: solo si la persona NO tiene la app abierta (si la tiene, ya lo
/// vio en tiempo real) y nunca con el texto de un mensaje privado (la pantalla bloqueada la puede leer cualquiera).
/// </summary>
public interface IAvisosPush
{
    Task NotificacionAsync(Guid usuarioId, string tipo, string mensaje);
    Task MensajeChatAsync(Guid receptorId, Guid conversacionId, string nombreAutor);
}

public sealed class AvisosPush : IAvisosPush
{
    private static readonly HashSet<string> TiposIntercambio = new(StringComparer.Ordinal)
    {
        TiposNotificacion.SolicitudNueva, TiposNotificacion.SolicitudAceptada, TiposNotificacion.SolicitudRechazada,
        TiposNotificacion.SolicitudCancelada, TiposNotificacion.EntregaConfirmada, TiposNotificacion.EntregaPendiente, TiposNotificacion.IntercambioCompletado,
        TiposNotificacion.IntercambioNoConcretado, TiposNotificacion.CalificacionRecibida
    };

    private readonly ColaPush _cola;
    private readonly IPresencia _presencia;
    private readonly PushOpciones _o;
    private readonly ILogger<AvisosPush> _log;

    public AvisosPush(ColaPush cola, IPresencia presencia, IOptions<PushOpciones> o, ILogger<AvisosPush> log)
    {
        _cola = cola; _presencia = presencia; _o = o.Value; _log = log;
    }

    private async Task<bool> DebeAvisarAsync(Guid usuarioId)
        => _o.Habilitado && !(await _presencia.EnLineaAsync(new[] { usuarioId })).Contains(usuarioId);

    public async Task NotificacionAsync(Guid usuarioId, string tipo, string mensaje)
    {
        try
        {
            if (!await DebeAvisarAsync(usuarioId)) return;
            var (titulo, ruta) = tipo == TiposNotificacion.BusquedaGuardada ? ("Novedades en tus búsquedas", "cuenta/busquedas")
                : tipo == TiposNotificacion.ComentarioNuevo ? ("Nuevo comentario en tu publicación", "notificaciones")
                : TiposIntercambio.Contains(tipo) ? ("Novedades en tu intercambio", "intercambios")
                : ("Trueke Bogotá Solidario", "notificaciones");
            _cola.Encolar(new AvisoPush(usuarioId, titulo, mensaje, ruta, $"aviso-{tipo}"));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo preparar el aviso push {Tipo} para {UsuarioId}", tipo, usuarioId);
        }
    }

    public async Task MensajeChatAsync(Guid receptorId, Guid conversacionId, string nombreAutor)
    {
        try
        {
            if (!await DebeAvisarAsync(receptorId)) return;
            // Una sola notificación por conversación: la nueva reemplaza a la anterior (etiqueta), sin llenar la pantalla.
            _cola.Encolar(new AvisoPush(receptorId, $"Mensaje de {nombreAutor}", "Toca para abrir el chat y responder.",
                $"mensajes/{conversacionId}", $"chat-{conversacionId:N}"));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo preparar el aviso push de mensaje para {UsuarioId}", receptorId);
        }
    }
}
