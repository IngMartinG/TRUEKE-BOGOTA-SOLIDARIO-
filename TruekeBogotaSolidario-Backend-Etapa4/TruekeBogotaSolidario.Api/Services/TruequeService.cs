using TruekeBogotaSolidario.Api.Common.Exceptions;
using TruekeBogotaSolidario.Api.Domain.Entities;
using TruekeBogotaSolidario.Api.Domain.Enums;
using TruekeBogotaSolidario.Api.DTOs.Solicitudes;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;
using TruekeBogotaSolidario.Api.Services.Interfaces;

namespace TruekeBogotaSolidario.Api.Services;

// ==========================================================================
// CAPA DE SERVICIO / LOGICA DE NEGOCIO - Implementacion
// Equivalente al "TruequeService" del Modelo C4 - Nivel 3.
// Orquesta Usuario + Publicacion + Solicitud para resolver un intercambio.
// ==========================================================================
public class TruequeService : ITruequeService
{
    private readonly ISolicitudRepository _solicitudRepository;
    private readonly IPublicacionRepository _publicacionRepository;

    public TruequeService(ISolicitudRepository solicitudRepository, IPublicacionRepository publicacionRepository)
    {
        _solicitudRepository = solicitudRepository;
        _publicacionRepository = publicacionRepository;
    }

    public async Task<List<SolicitudResponseDto>> ObtenerPorPublicacionAsync(Guid publicacionId)
    {
        var solicitudes = await _solicitudRepository.ObtenerPorPublicacionAsync(publicacionId);
        return solicitudes.Select(MapearADto).ToList();
    }

    public async Task<SolicitudResponseDto> SolicitarIntercambioAsync(CrearSolicitudDto dto)
    {
        var publicacion = await _publicacionRepository.ObtenerPorIdAsync(dto.PublicacionId)
            ?? throw new NotFoundException($"No existe una publicacion con id '{dto.PublicacionId}'.");

        // Regla de negocio: solo se puede solicitar trueke sobre algo DISPONIBLE.
        if (publicacion.Estado != EstadoPublicacionEnum.DISPONIBLE)
        {
            throw new BusinessRuleException(
                $"La publicacion '{publicacion.Titulo}' no esta DISPONIBLE (estado actual: {publicacion.Estado}).");
        }

        var usuario = await _publicacionRepository.ObtenerUsuarioPorIdAsync(dto.UsuarioId)
            ?? throw new BusinessRuleException($"El usuario '{dto.UsuarioId}' no existe.");

        // Se delega en el metodo de dominio de Usuario (Modelo C4 - Nivel 4),
        // que ademas valida que nadie solicite su propia publicacion.
        var solicitud = usuario.SolicitarIntercambio(publicacion, dto.Mensaje.Trim());

        // Al llegar la primera solicitud formal, la publicacion pasa a EN_NEGOCIACION.
        publicacion.MarcarEnNegociacion();

        await _solicitudRepository.AgregarAsync(solicitud);
        await _solicitudRepository.GuardarCambiosAsync();

        solicitud.Usuario = usuario;
        solicitud.Publicacion = publicacion;

        return MapearADto(solicitud);
    }

    public async Task<SolicitudResponseDto> AceptarAsync(Guid solicitudId)
    {
        var solicitud = await _solicitudRepository.ObtenerPorIdAsync(solicitudId)
            ?? throw new NotFoundException($"No existe una solicitud con id '{solicitudId}'.");

        solicitud.Aceptar();
        solicitud.Publicacion?.ConfirmarIntercambio();

        await _solicitudRepository.GuardarCambiosAsync();
        return MapearADto(solicitud);
    }

    public async Task<SolicitudResponseDto> RechazarAsync(Guid solicitudId, RechazarSolicitudDto dto)
    {
        var solicitud = await _solicitudRepository.ObtenerPorIdAsync(solicitudId)
            ?? throw new NotFoundException($"No existe una solicitud con id '{solicitudId}'.");

        solicitud.Rechazar(dto.Motivo);

        // La publicacion vuelve a estar disponible para otras solicitudes.
        solicitud.Publicacion?.Publicar();

        await _solicitudRepository.GuardarCambiosAsync();
        return MapearADto(solicitud);
    }

    private static SolicitudResponseDto MapearADto(Solicitud s) => new(
        s.Id,
        s.FechaSolicitud,
        s.Mensaje,
        s.Aceptada,
        s.Rechazada,
        s.UsuarioId,
        s.Usuario?.NombreCompleto,
        s.PublicacionId,
        s.Publicacion?.Titulo);
}
