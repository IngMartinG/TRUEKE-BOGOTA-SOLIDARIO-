using TruekeBogotaSolidario.Api.Common.Exceptions;
using TruekeBogotaSolidario.Api.Domain.Enums;

namespace TruekeBogotaSolidario.Api.Domain.Entities;

// ==========================================================================
// CAPA DE DOMINIO - Entidad Publicacion (entidad principal del dominio)
// Objeto o servicio que un usuario ofrece para intercambiar. Su estructura
// y comportamiento (maquina de estados) reproducen fielmente el diagrama
// PlantUML del Modelo C4 - Nivel 4 del proyecto Trueke Bogota Solidario.
// ==========================================================================
public class Publicacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Titulo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public EstadoPublicacionEnum Estado { get; set; } = EstadoPublicacionEnum.DISPONIBLE;
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;

    // Relaciones (Publicacion "*" --> "1" Categoria, Usuario "1" --> "*" Publicacion)
    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public ICollection<Solicitud> Solicitudes { get; set; } = new List<Solicitud>();

    // ---------------------------------------------------------------------
    // Transiciones de estado (comportamiento del dominio, no CRUD plano).
    // Cada metodo valida que la transicion sea coherente con el estado actual.
    // ---------------------------------------------------------------------
    public void Publicar()
    {
        Estado = EstadoPublicacionEnum.DISPONIBLE;
    }

    public void MarcarEnNegociacion()
    {
        if (Estado != EstadoPublicacionEnum.DISPONIBLE)
        {
            throw new BusinessRuleException(
                $"Solo una publicacion DISPONIBLE puede pasar a EN_NEGOCIACION (estado actual: {Estado}).");
        }

        Estado = EstadoPublicacionEnum.EN_NEGOCIACION;
    }

    public void ConfirmarIntercambio()
    {
        if (Estado != EstadoPublicacionEnum.EN_NEGOCIACION)
        {
            throw new BusinessRuleException(
                $"Solo una publicacion EN_NEGOCIACION puede confirmarse como INTERCAMBIADA (estado actual: {Estado}).");
        }

        Estado = EstadoPublicacionEnum.INTERCAMBIADA;
    }

    public void Cancelar(string motivo)
    {
        if (Estado == EstadoPublicacionEnum.INTERCAMBIADA)
        {
            throw new BusinessRuleException("No se puede cancelar una publicacion ya INTERCAMBIADA.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new BusinessRuleException("Debe indicar un motivo para cancelar la publicacion.");
        }

        Estado = EstadoPublicacionEnum.CANCELADA;
    }
}
