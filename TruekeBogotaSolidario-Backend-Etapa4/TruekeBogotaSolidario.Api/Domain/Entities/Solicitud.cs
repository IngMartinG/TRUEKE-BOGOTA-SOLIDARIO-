using TruekeBogotaSolidario.Api.Common.Exceptions;

namespace TruekeBogotaSolidario.Api.Domain.Entities;

// ==========================================================================
// CAPA DE DOMINIO - Entidad Solicitud
// Representa cuando un Usuario le pide a otro intercambiar por una
// publicacion especifica (Solicitud "*" --> "1" Publicacion : referencia).
// ==========================================================================
public class Solicitud
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;
    public string Mensaje { get; set; } = string.Empty;
    public bool Aceptada { get; set; }
    public bool Rechazada { get; private set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public Guid PublicacionId { get; set; }
    public Publicacion? Publicacion { get; set; }

    public void Aceptar()
    {
        if (Aceptada || Rechazada)
        {
            throw new BusinessRuleException("La solicitud ya fue resuelta previamente.");
        }

        Aceptada = true;
    }

    public void Rechazar(string motivo)
    {
        if (Aceptada || Rechazada)
        {
            throw new BusinessRuleException("La solicitud ya fue resuelta previamente.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new BusinessRuleException("Debe indicar un motivo para rechazar la solicitud.");
        }

        Rechazada = true;
    }
}
