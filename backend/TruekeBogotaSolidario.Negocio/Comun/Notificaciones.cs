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
    /// <summary>La otra parte contraofertó: ahora te toca aceptar, contraofertar o retirarte (RecursoId = SolicitudId).</summary>
    public const string SolicitudContraoferta = "SolicitudContraoferta";
    /// <summary>El dueño aceptó a otra persona: la solicitud queda en lista de espera por si no se concreta.</summary>
    public const string SolicitudEnEspera = "SolicitudEnEspera";
    /// <summary>El intercambio con otra persona no se concretó: la publicación volvió y el dueño puede elegir de la lista.</summary>
    public const string SolicitudDisponibleDeNuevo = "SolicitudDisponibleDeNuevo";
    public const string PagoAprobado = "PagoAprobado";
    public const string PagoRechazado = "PagoRechazado";
    public const string PagoEnRevision = "PagoEnRevision";
    public const string PublicacionOcultada = "PublicacionOcultada";
    public const string PublicacionRestaurada = "PublicacionRestaurada";
    /// <summary>Al dueño: su publicación de riesgo espera a un moderador antes de entrar al catálogo (RecursoId = PublicacionId).</summary>
    public const string PublicacionEnRevision = "PublicacionEnRevision";
    public const string PublicacionAprobada = "PublicacionAprobada";
    /// <summary>Al dueño, con lo que debe corregir: al editarla vuelve a revisión.</summary>
    public const string PublicacionRechazada = "PublicacionRechazada";
    /// <summary>Al dueño, cuando otra persona comenta su publicación (RecursoId = PublicacionId).</summary>
    public const string ComentarioNuevo = "ComentarioNuevo";
    public const string ComentarioOcultado = "ComentarioOcultado";
    public const string MensajeOcultado = "MensajeOcultado";
    public const string VerificacionAprobada = "VerificacionAprobada";
    public const string VerificacionRechazada = "VerificacionRechazada";
    /// <summary>La foto de perfil no muestra el rostro: hay que cambiarla antes del plazo (también llega por correo).</summary>
    public const string FotoPerfilRechazada = "FotoPerfilRechazada";
    /// <summary>Un moderador aprobó la foto que el detector dejó en revisión.</summary>
    public const string FotoPerfilAprobada = "FotoPerfilAprobada";
    public const string DenunciaRevisada = "DenunciaRevisada";
    /// <summary>Al denunciado, cuando un moderador consideró procedente una denuncia en su contra (RecursoId = ResolucionId).</summary>
    public const string DenunciaRecibida = "DenunciaRecibida";
    public const string ApelacionResuelta = "ApelacionResuelta";
    public const string EntregaConfirmada = "EntregaConfirmada";
    /// <summary>Recordatorio a quien no ha confirmado la entrega de un intercambio aceptado (RecursoId = SolicitudId).</summary>
    public const string EntregaPendiente = "EntregaPendiente";
    /// <summary>La moderación revisó la cuenta en revisión por mayoría de edad y descartó la marca.</summary>
    public const string CuentaRevisada = "CuentaRevisada";
    public const string IntercambioCompletado = "IntercambioCompletado";
    /// <summary>Invita y ganan los dos: a ambos, cuando la persona invitada completa su primer intercambio.</summary>
    public const string InvitacionRecompensada = "InvitacionRecompensada";
    /// <summary>La persona alcanzó un nivel nuevo de Eco-Puntos (Brote, Árbol, Bosque o Guardián).</summary>
    public const string NivelAlcanzado = "NivelAlcanzado";
    /// <summary>Bono de una sola vez: perfil completo o primera publicación (RecursoId = PublicacionId en la primera publicación).</summary>
    public const string EcoPuntosGanados = "EcoPuntosGanados";
    public const string IntercambioNoConcretado = "IntercambioNoConcretado";
    public const string CalificacionRecibida = "CalificacionRecibida";
    public const string PagoReembolsado = "PagoReembolsado";
    public const string PlanPorVencer = "PlanPorVencer";
    public const string FacturaEmitida = "FacturaEmitida";
    public const string PqrRespondida = "PqrRespondida";
    public const string BusquedaGuardada = "BusquedaGuardada";
    /// <summary>Alguien publicó algo de la lista de deseos (RecursoId = PublicacionId si es una sola; sin recurso si son varias).</summary>
    public const string DeseoCoincidencia = "DeseoCoincidencia";
    public const string FundacionAprobada = "FundacionAprobada";
    public const string FundacionRechazada = "FundacionRechazada";
    /// <summary>La moderación retiró la insignia de fundación verificada (el motivo se ve en la cuenta).</summary>
    public const string FundacionRetirada = "FundacionRetirada";
    /// <summary>A la fundación, cuando alguien publica una donación desde "Tengo esto" en una de sus necesidades (RecursoId = PublicacionId).</summary>
    public const string DonacionParaFundacion = "DonacionParaFundacion";
    // Solo para moderadores
    public const string AlertaPagoEnRevision = "AlertaPagoEnRevision";
    public const string PqrNueva = "PqrNueva";
    public const string ApelacionNueva = "ApelacionNueva";
    /// <summary>Una cuenta existente declaró una fecha de menor de edad (RecursoId = UsuarioId): revisarla.</summary>
    public const string MenorDetectado = "MenorDetectado";
    /// <summary>Una cuenta pidió verificarse como fundación (RecursoId = UsuarioId): la revisa un SuperUsuario.</summary>
    public const string FundacionPorRevisar = "FundacionPorRevisar";
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

/// <summary>Al autor: sus mensajes de la conversación hasta esas fechas ya llegaron (✓✓) o ya se leyeron (✓✓ de color).</summary>
public sealed record EstadoMensajesDto(Guid ConversacionId, DateTime? EntregadosHastaUtc, DateTime? LeidosHastaUtc);
public sealed record EscribiendoDto(Guid ConversacionId);
public sealed record PresenciaDto(Guid UsuarioId, bool EnLinea);

/// <summary>Canal en tiempo real (SignalR en Presentacion). Por defecto no hace nada (pruebas de Negocio, workers).</summary>
public interface IEmisorTiempoReal
{
    Task NotificacionAsync(Guid usuarioId, NotificacionDto notificacion);
    Task MensajeChatAsync(Guid usuarioId, MensajeChatDto mensaje);
    Task EstadoMensajesAsync(Guid usuarioId, EstadoMensajesDto estado);
    Task EscribiendoAsync(Guid usuarioId, EscribiendoDto escribiendo);
    Task PresenciaAsync(IReadOnlyCollection<Guid> usuarioIds, PresenciaDto presencia);
    /// <summary>A los moderadores: cambió lo que espera revisión (contador del panel).</summary>
    Task ColaModeracionAsync(IReadOnlyCollection<Guid> moderadorIds, ColaModeracionDto cola);
}

public sealed class EmisorTiempoRealNulo : IEmisorTiempoReal
{
    public Task NotificacionAsync(Guid usuarioId, NotificacionDto notificacion) => Task.CompletedTask;
    public Task MensajeChatAsync(Guid usuarioId, MensajeChatDto mensaje) => Task.CompletedTask;
    public Task EstadoMensajesAsync(Guid usuarioId, EstadoMensajesDto estado) => Task.CompletedTask;
    public Task EscribiendoAsync(Guid usuarioId, EscribiendoDto escribiendo) => Task.CompletedTask;
    public Task PresenciaAsync(IReadOnlyCollection<Guid> usuarioIds, PresenciaDto presencia) => Task.CompletedTask;
    public Task ColaModeracionAsync(IReadOnlyCollection<Guid> moderadorIds, ColaModeracionDto cola) => Task.CompletedTask;
}

public sealed class NotificadorPersistente : INotificador
{
    private readonly INotificacionRepository _repo;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IEmisorTiempoReal _emisor;
    private readonly IAvisosCorreo _avisosCorreo;
    private readonly Push.IAvisosPush _avisosPush;
    private readonly TimeProvider _reloj;
    private readonly ILogger<NotificadorPersistente> _log;

    public NotificadorPersistente(INotificacionRepository repo, IUnidadDeTrabajo uow, IEmisorTiempoReal emisor, IAvisosCorreo avisosCorreo,
        Push.IAvisosPush avisosPush,
        TimeProvider reloj, ILogger<NotificadorPersistente> log)
    {
        _repo = repo; _uow = uow; _emisor = emisor; _avisosCorreo = avisosCorreo; _avisosPush = avisosPush; _reloj = reloj; _log = log;
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
        // Si no tiene la app abierta: también por correo (según sus preferencias) y push a sus dispositivos (nunca falla la operación)
        await _avisosCorreo.NotificacionAsync(usuarioId, tipo, n.Mensaje, recursoId);
        await _avisosPush.NotificacionAsync(usuarioId, tipo, n.Mensaje, recursoId);
    }

    public static NotificacionDto ADto(Notificacion n) => new(n.Id, n.Tipo, n.Mensaje, n.RecursoId, n.FechaUtc, n.LeidaUtc is not null);
}
