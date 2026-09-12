using TruekeBogotaSolidario.Api.DTOs.Solicitudes;

namespace TruekeBogotaSolidario.Api.Services.Interfaces;

// ==========================================================================
// CAPA DE SERVICIO / LOGICA DE NEGOCIO - Contrato
// Equivalente al "TruequeService" del Modelo C4 - Nivel 3:
// contiene las reglas de "¿este usuario puede solicitar este trueke?" y
// "¿este producto ya fue intercambiado?".
// ==========================================================================
public interface ITruequeService
{
    Task<List<SolicitudResponseDto>> ObtenerPorPublicacionAsync(Guid publicacionId);
    Task<SolicitudResponseDto> SolicitarIntercambioAsync(CrearSolicitudDto dto);
    Task<SolicitudResponseDto> AceptarAsync(Guid solicitudId);
    Task<SolicitudResponseDto> RechazarAsync(Guid solicitudId, RechazarSolicitudDto dto);
}
