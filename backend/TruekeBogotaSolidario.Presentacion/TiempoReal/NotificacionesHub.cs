using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Presentacion.TiempoReal;

/// <summary>
/// Hub de solo servidor → cliente: el navegador no puede invocar métodos (no hay ninguno público). Recibe los eventos
/// <see cref="EventoNotificacion"/> y <see cref="EventoMensaje"/>. Cada usuario recibe ÚNICAMENTE lo suyo (Clients.User por claim "sub").
/// Enviar mensajes de chat se hace por HTTP (POST /conversaciones/{id}/mensajes), donde se valida y persiste.
/// </summary>
[Authorize]
public sealed class NotificacionesHub : Hub
{
    public const string Ruta = "/hubs/notificaciones";
    public const string EventoNotificacion = "notificacion";
    public const string EventoMensaje = "mensaje";
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

    public Task NotificacionAsync(Guid usuarioId, NotificacionDto notificacion)
        => _hub.Clients.User(usuarioId.ToString()).SendAsync(NotificacionesHub.EventoNotificacion, notificacion);

    public Task MensajeChatAsync(Guid usuarioId, MensajeChatDto mensaje)
        => _hub.Clients.User(usuarioId.ToString()).SendAsync(NotificacionesHub.EventoMensaje, mensaje);
}
