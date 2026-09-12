using System.ComponentModel.DataAnnotations;

namespace TruekeBogotaSolidario.Api.DTOs.Usuarios;

public record UsuarioResponseDto(
    Guid Id,
    string NombreCompleto,
    string Localidad,
    string Correo);

public class CrearUsuarioDto
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150, MinimumLength = 3)]
    public string NombreCompleto { get; set; } = string.Empty;

    [Required(ErrorMessage = "La localidad es obligatoria.")]
    public string Localidad { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato valido.")]
    public string Correo { get; set; } = string.Empty;
}
