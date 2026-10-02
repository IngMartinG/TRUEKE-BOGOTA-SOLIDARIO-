using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public class Solicitud
{
    private Solicitud() { Mensaje = ""; } // EF Core

    public Solicitud(Guid publicacionId, Guid solicitanteId, string mensaje)
    {
        mensaje = (mensaje ?? "").Trim();
        if (mensaje.Length is < 1 or > 500) throw new ReglaDeNegocioException("El mensaje es obligatorio (máximo 500 caracteres).");
        Id = Guid.NewGuid();
        PublicacionId = publicacionId;
        SolicitanteId = solicitanteId;
        Mensaje = mensaje;
        FechaSolicitud = DateTime.UtcNow;
        Estado = EstadoSolicitud.Pendiente;
    }

    public Guid Id { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Publicacion? Publicacion { get; private set; }
    public Guid SolicitanteId { get; private set; }
    public Usuario? Solicitante { get; private set; }
    public DateTime FechaSolicitud { get; private set; }
    public string Mensaje { get; private set; }
    public EstadoSolicitud Estado { get; private set; }
    public string? MotivoRechazo { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void Aceptar()
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Aceptada;
    }

    public void Rechazar(string motivo)
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Rechazada;
        MotivoRechazo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }

    public void Cancelar()
    {
        ExigirPendiente();
        Estado = EstadoSolicitud.Cancelada;
    }

    private void ExigirPendiente()
    {
        if (Estado != EstadoSolicitud.Pendiente) throw new ReglaDeNegocioException("La solicitud ya fue resuelta.");
    }
}
