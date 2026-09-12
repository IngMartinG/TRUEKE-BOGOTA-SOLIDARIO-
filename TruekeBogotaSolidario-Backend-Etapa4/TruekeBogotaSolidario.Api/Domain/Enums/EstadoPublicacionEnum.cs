namespace TruekeBogotaSolidario.Api.Domain.Enums;

// ==========================================================================
// CAPA DE DOMINIO
// Enum tomado tal cual del Modelo C4 - Nivel 4 (Codigo) del proyecto.
// Evita que una Publicacion quede en un estado invalido como "medio disponible".
// ==========================================================================
public enum EstadoPublicacionEnum
{
    DISPONIBLE = 0,
    EN_NEGOCIACION = 1,
    INTERCAMBIADA = 2,
    CANCELADA = 3
}
