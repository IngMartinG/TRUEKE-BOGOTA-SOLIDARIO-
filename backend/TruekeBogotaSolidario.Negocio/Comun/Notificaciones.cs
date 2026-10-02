namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Tipos de notificación en tiempo real (el front los usa como discriminador).</summary>
public static class TiposNotificacion
{
    public const string SolicitudNueva = "SolicitudNueva";
    public const string SolicitudAceptada = "SolicitudAceptada";
    public const string SolicitudRechazada = "SolicitudRechazada";
    public const string SolicitudCancelada = "SolicitudCancelada";
    public const string PagoAprobado = "PagoAprobado";
    public const string PagoRechazado = "PagoRechazado";
    public const string PagoEnRevision = "PagoEnRevision";
    public const string PublicacionOcultada = "PublicacionOcultada";
    public const string PublicacionRestaurada = "PublicacionRestaurada";
    public const string ComentarioOcultado = "ComentarioOcultado";
    public const string VerificacionAprobada = "VerificacionAprobada";
    public const string VerificacionRechazada = "VerificacionRechazada";
}

/// <summary>Carga mínima: el front pide el detalle al API (con su JWT) si lo necesita. Nunca datos de terceros.</summary>
public sealed record NotificacionDto(string Tipo, string Mensaje, Guid? RecursoId, DateTime FechaUtc);

/// <summary>
/// Envía notificaciones a un usuario. Se llama SIEMPRE después de confirmar los cambios en la base de datos y nunca debe
/// romper la operación de negocio (las implementaciones atrapan y registran sus propios errores).
/// </summary>
public interface INotificador
{
    Task NotificarAsync(Guid usuarioId, NotificacionDto notificacion);
}

/// <summary>Implementación por defecto (pruebas, workers sin SignalR). Presentacion la reemplaza por la de SignalR.</summary>
public sealed class NotificadorNulo : INotificador
{
    public Task NotificarAsync(Guid usuarioId, NotificacionDto notificacion) => Task.CompletedTask;
}
