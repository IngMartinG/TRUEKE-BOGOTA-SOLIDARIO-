using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Ciclo: Pendiente → (Aceptada → Completada | NoConcretada) | Rechazada | Cancelada.
/// Aceptar NO completa el intercambio: las partes coordinan por chat y ambas confirman la entrega. Los Eco-Puntos se
/// otorgan al completarse (cuando las dos confirman, o automáticamente si una confirmó y pasan los días de gracia).
/// </summary>
public class Solicitud
{
    private Solicitud() { Mensaje = ""; } // EF Core

    public Solicitud(Guid publicacionId, Guid solicitanteId, string mensaje, Guid? publicacionOfrecidaId = null)
    {
        mensaje = (mensaje ?? "").Trim();
        if (mensaje.Length is < 1 or > 500) throw new ReglaDeNegocioException("El mensaje es obligatorio (máximo 500 caracteres).");
        Id = Guid.NewGuid();
        PublicacionId = publicacionId;
        PublicacionOfrecidaId = publicacionOfrecidaId;
        SolicitanteId = solicitanteId;
        Mensaje = mensaje;
        FechaSolicitud = DateTime.UtcNow;
        Estado = EstadoSolicitud.Pendiente;
    }

    public Guid Id { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Publicacion? Publicacion { get; private set; }
    /// <summary>
    /// Trueke: publicación del solicitante que ofrece a cambio (opcional). Al aceptar, también queda reservada; al completarse,
    /// también se intercambia; si no se concreta, vuelve al catálogo.
    /// </summary>
    public Guid? PublicacionOfrecidaId { get; private set; }
    public Publicacion? PublicacionOfrecida { get; private set; }
    public Guid SolicitanteId { get; private set; }
    public Usuario? Solicitante { get; private set; }
    public DateTime FechaSolicitud { get; private set; }
    public string Mensaje { get; private set; }
    public EstadoSolicitud Estado { get; private set; }
    /// <summary>Motivo de rechazo o de "no concretada".</summary>
    public string? MotivoRechazo { get; private set; }
    public DateTime? FechaAceptacionUtc { get; private set; }
    public DateTime? ConfirmadaPorDuenioUtc { get; private set; }
    public DateTime? ConfirmadaPorSolicitanteUtc { get; private set; }
    public DateTime? FechaCierreUtc { get; private set; }
    /// <summary>Aviso de "¿ya se hizo la entrega?" a quien no había confirmado (una sola vez).</summary>
    public DateTime? RecordatorioEntregaUtc { get; private set; }
    /// <summary>Aviso de "mañana se cierra solo" a quien no había confirmado (una sola vez).</summary>
    public DateTime? RecordatorioCierreUtc { get; private set; }
    public uint RowVersion { get; private set; }

    public bool EstaEnCurso => Estado is EstadoSolicitud.Pendiente or EstadoSolicitud.Aceptada;

    public void Aceptar(DateTime ahoraUtc)
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Aceptada;
        FechaAceptacionUtc = ahoraUtc;
    }

    public void Rechazar(string motivo)
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Rechazada;
        MotivoRechazo = Recortar(motivo);
        FechaCierreUtc = DateTime.UtcNow;
    }

    /// <param name="motivo">Si la cancela el sistema (p. ej. el objeto ofrecido dejó de estar disponible).</param>
    public void Cancelar(string? motivo = null)
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Cancelada;
        MotivoRechazo = Recortar(motivo);
        FechaCierreUtc = DateTime.UtcNow;
    }

    /// <returns>true si con esta confirmación ya confirmaron ambas partes.</returns>
    public bool ConfirmarEntrega(bool esDuenio, DateTime ahoraUtc)
    {
        if (Estado != EstadoSolicitud.Aceptada) throw new ReglaDeNegocioException("Solo se confirma la entrega de una solicitud aceptada.");
        if (esDuenio)
        {
            if (ConfirmadaPorDuenioUtc is not null) throw new ReglaDeNegocioException("Ya confirmaste la entrega.");
            ConfirmadaPorDuenioUtc = ahoraUtc;
        }
        else
        {
            if (ConfirmadaPorSolicitanteUtc is not null) throw new ReglaDeNegocioException("Ya confirmaste la entrega.");
            ConfirmadaPorSolicitanteUtc = ahoraUtc;
        }
        return ConfirmadaPorDuenioUtc is not null && ConfirmadaPorSolicitanteUtc is not null;
    }

    public void Completar(DateTime ahoraUtc)
    {
        if (Estado != EstadoSolicitud.Aceptada) throw new ReglaDeNegocioException("La solicitud no está aceptada.");
        Estado = EstadoSolicitud.Completada;
        FechaCierreUtc = ahoraUtc;
    }

    /// <summary>El intercambio acordado no ocurrió (cualquiera de las partes lo informa, o vence sin confirmaciones).</summary>
    public void MarcarNoConcretada(string motivo, DateTime ahoraUtc)
    {
        if (Estado != EstadoSolicitud.Aceptada) throw new ReglaDeNegocioException("Solo una solicitud aceptada puede marcarse como no concretada.");
        Estado = EstadoSolicitud.NoConcretada;
        MotivoRechazo = Recortar(motivo);
        FechaCierreUtc = ahoraUtc;
    }

    public void MarcarRecordatorioEntrega(DateTime ahoraUtc) => RecordatorioEntregaUtc ??= ahoraUtc;

    /// <summary>El de cierre reemplaza al de entrega si aún no había salido: nadie recibe los dos el mismo día.</summary>
    public void MarcarRecordatorioCierre(DateTime ahoraUtc)
    {
        RecordatorioCierreUtc ??= ahoraUtc;
        RecordatorioEntregaUtc ??= ahoraUtc;
    }

    private static string? Recortar(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) return null;
        var m = motivo.Trim();
        return m.Length > 300 ? m[..300] : m;
    }

    private void ExigirPendiente()
    {
        if (Estado != EstadoSolicitud.Pendiente) throw new ReglaDeNegocioException("La solicitud ya fue resuelta.");
    }
}
