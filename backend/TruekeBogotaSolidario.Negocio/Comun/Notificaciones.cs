using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Tipos de notificación (el front los usa como discriminador).</summary>
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
    public const string MensajeOcultado = "MensajeOcultado";
    public const string VerificacionAprobada = "VerificacionAprobada";
    public const string VerificacionRechazada = "VerificacionRechazada";
    public const string DenunciaRevisada = "DenunciaRevisada";
    public const string EntregaConfirmada = "EntregaConfirmada";
    public const string IntercambioCompletado = "IntercambioCompletado";
    public const string IntercambioNoConcretado = "IntercambioNoConcretado";
    public const string CalificacionRecibida = "CalificacionRecibida";
    public const string PagoReembolsado = "PagoReembolsado";
    public const string PlanPorVencer = "PlanPorVencer";
    public const string FacturaEmitida = "FacturaEmitida";
    public const string PqrRespondida = "PqrRespondida";
    // Solo para moderadores
    public const string AlertaPagoEnRevision = "AlertaPagoEnRevision";
    public const string PqrNueva = "PqrNueva";
}

/// <summary>Carga mínima: el front pide el detalle al API (con su JWT) si lo necesita. Nunca datos de terceros.</summary>
public sealed record NotificacionDto(Guid Id, string Tipo, string Mensaje, Guid? RecursoId, DateTime FechaUtc, bool Leida);

/// <summary>
/// Notifica a un usuario: guarda en su bandeja y lo empuja en tiempo real si está conectado.
/// Se llama SIEMPRE después de confirmar la operación de negocio y nunca la hace fallar.
/// </summary>
public interface INotificador
{
    Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid? recursoId = null);
}

/// <summary>Canal en tiempo real (SignalR en Presentacion). Por defecto no hace nada (pruebas de Negocio, workers).</summary>
public interface IEmisorTiempoReal
{
    Task NotificacionAsync(Guid usuarioId, NotificacionDto notificacion);
    Task MensajeChatAsync(Guid usuarioId, MensajeChatDto mensaje);
}

public sealed class EmisorTiempoRealNulo : IEmisorTiempoReal
{
    public Task NotificacionAsync(Guid usuarioId, NotificacionDto notificacion) => Task.CompletedTask;
    public Task MensajeChatAsync(Guid usuarioId, MensajeChatDto mensaje) => Task.CompletedTask;
}

public sealed class NotificadorPersistente : INotificador
{
    private readonly INotificacionRepository _repo;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IEmisorTiempoReal _emisor;
    private readonly TimeProvider _reloj;
    private readonly ILogger<NotificadorPersistente> _log;

    public NotificadorPersistente(INotificacionRepository repo, IUnidadDeTrabajo uow, IEmisorTiempoReal emisor, TimeProvider reloj,
        ILogger<NotificadorPersistente> log)
    {
        _repo = repo; _uow = uow; _emisor = emisor; _reloj = reloj; _log = log;
    }

    public async Task NotificarAsync(Guid usuarioId, string tipo, string mensaje, Guid? recursoId = null)
    {
        var n = new Notificacion(usuarioId, tipo, mensaje, recursoId, _reloj.GetUtcNow().UtcDateTime);
        try
        {
            _repo.Agregar(n);
            await _uow.GuardarCambiosAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo guardar la notificación {Tipo} del usuario {UsuarioId}", tipo, usuarioId);
        }
        try
        {
            await _emisor.NotificacionAsync(usuarioId, ADto(n));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "No se pudo enviar en tiempo real la notificación {Tipo} al usuario {UsuarioId}", tipo, usuarioId);
        }
    }

    public static NotificacionDto ADto(Notificacion n) => new(n.Id, n.Tipo, n.Mensaje, n.RecursoId, n.FechaUtc, n.LeidaUtc is not null);
}
