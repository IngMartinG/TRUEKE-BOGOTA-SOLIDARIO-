using System.ComponentModel.DataAnnotations;

namespace TruekeBogotaSolidario.Api.DTOs.Solicitudes;

public record SolicitudResponseDto(
    Guid Id,
    DateTime FechaSolicitud,
    string Mensaje,
    bool Aceptada,
    bool Rechazada,
    Guid UsuarioId,
    string? NombreUsuario,
    Guid PublicacionId,
    string? TituloPublicacion);

public class CrearSolicitudDto
{
    [Required(ErrorMessage = "El usuario que solicita es obligatorio.")]
    public Guid UsuarioId { get; set; }

    [Required(ErrorMessage = "La publicacion a intercambiar es obligatoria.")]
    public Guid PublicacionId { get; set; }

    [Required(ErrorMessage = "El mensaje de la solicitud es obligatorio.")]
    [StringLength(500, MinimumLength = 5)]
    public string Mensaje { get; set; } = string.Empty;
}

public class RechazarSolicitudDto
{
    [Required(ErrorMessage = "Debe indicar el motivo del rechazo.")]
    [StringLength(300, MinimumLength = 3)]
    public string Motivo { get; set; } = string.Empty;
}
