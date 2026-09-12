using TruekeBogotaSolidario.Api.DTOs.Publicaciones;

namespace TruekeBogotaSolidario.Api.Services.Interfaces;

// ==========================================================================
// CAPA DE SERVICIO / LOGICA DE NEGOCIO - Contrato
// Equivalente al "ListingService" del Modelo C4 - Nivel 3.
// ==========================================================================
public interface IPublicacionService
{
    Task<List<PublicacionResponseDto>> ObtenerTodasAsync();
    Task<PublicacionResponseDto> ObtenerPorIdAsync(Guid id);
    Task<PublicacionResponseDto> CrearAsync(CrearPublicacionDto dto);
    Task<PublicacionResponseDto> CambiarEstadoAsync(Guid id, ActualizarEstadoPublicacionDto dto);
}
