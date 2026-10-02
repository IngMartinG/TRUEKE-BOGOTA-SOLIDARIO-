using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Presentacion.TiempoReal;

/// <summary>
/// Hub de solo servidor → cliente: el navegador no puede invocar métodos (no hay ninguno público), solo recibe
/// el evento <see cref="EventoNotificacion"/>. Cada usuario recibe ÚNICAMENTE sus propias notificaciones (Clients.User por claim "sub").
/// </summary>
[Authorize]
public sealed class NotificacionesHub : Hub
{
    public const string Ruta = "/hubs/notificaciones";
    public const string EventoNotificacion = "notificacion";
}

/// <summary>Identifica la conexión con el claim "sub" del JWT (no con el nombre ni el correo).</summary>
public sealed class UsuarioIdPorSub : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}

/// <summary>Implementación real de <see cref="INotificador"/>. Con Redis habilitado, llega a usuarios conectados a cualquier instancia.</summary>
public sealed class NotificadorSignalR : INotificador
{
    private readonly IHubContext<NotificacionesHub> _hub;
    private readonly ILogger<NotificadorSignalR> _log;

    public NotificadorSignalR(IHubContext<NotificacionesHub> hub, ILogger<NotificadorSignalR> log)
    {
        _hub = hub;
        _log = log;
    }

    public async Task NotificarAsync(Guid usuarioId, NotificacionDto notificacion)
    {
        try
        {
            await _hub.Clients.User(usuarioId.ToString()).SendAsync(NotificacionesHub.EventoNotificacion, notificacion);
        }
        catch (Exception ex)
        {
            // La operación de negocio ya se confirmó: una notificación perdida no debe convertirse en un 500.
            _log.LogWarning(ex, "No se pudo enviar la notificación {Tipo} al usuario {UsuarioId}", notificacion.Tipo, usuarioId);
        }
    }
}
