using System.ComponentModel.DataAnnotations;
using TruekeBogotaSolidario.Api.Domain.Enums;

namespace TruekeBogotaSolidario.Api.DTOs.Publicaciones;

// ==========================================================================
// CAPA DE DOMINIO / DTOS
// DTOs separados para lectura y creacion, igual que en el patron de
// referencia (ProductoResponseDto / CrearProductoDto), aplicados a Publicacion.
// ==========================================================================

// DTO de LECTURA: lo que ve el cliente al consultar publicaciones.
public record PublicacionResponseDto(
    Guid Id,
    string Titulo,
    string Descripcion,
    EstadoPublicacionEnum Estado,
    DateTime FechaPublicacion,
    int CategoriaId,
    string? NombreCategoria,
    Guid UsuarioId,
    string? NombreUsuario);

// DTO de CREACION: lo que envia el cliente para publicar un nuevo bien.
public class CrearPublicacionDto
{
    [Required(ErrorMessage = "El titulo es obligatorio.")]
    [StringLength(120, MinimumLength = 3)]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripcion es obligatoria.")]
    [StringLength(1000, MinimumLength = 10)]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La categoria es obligatoria.")]
    public int CategoriaId { get; set; }

    [Required(ErrorMessage = "El usuario que publica es obligatorio.")]
    public Guid UsuarioId { get; set; }
}

// DTO para las transiciones de estado (Publicar / EnNegociacion / Confirmar / Cancelar).
public class ActualizarEstadoPublicacionDto
{
    [Required]
    public string Accion { get; set; } = string.Empty; // "en-negociacion" | "confirmar" | "cancelar"

    public string? Motivo { get; set; } // requerido solo cuando Accion == "cancelar"
}
