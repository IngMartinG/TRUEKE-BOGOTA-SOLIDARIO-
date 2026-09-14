using TruekeBogotaSolidario.Api.Common.Exceptions;
using TruekeBogotaSolidario.Api.Domain.Entities;
using TruekeBogotaSolidario.Api.DTOs.Publicaciones;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;
using TruekeBogotaSolidario.Api.Services.Interfaces;

namespace TruekeBogotaSolidario.Api.Services;

// ==========================================================================
// CAPA DE SERVICIO / LOGICA DE NEGOCIO - Implementacion
// Aqui viven las reglas reales del negocio: "¿este usuario puede publicar?",
// "¿la categoria existe?", "¿la transicion de estado solicitada es valida?".
// El Controller SOLO delega aqui; nunca habla directo con el Repository.
// ==========================================================================
public class PublicacionService : IPublicacionService
{
    private readonly IPublicacionRepository _publicacionRepository;

    public PublicacionService(IPublicacionRepository publicacionRepository)
    {
        _publicacionRepository = publicacionRepository;
    }

    public async Task<List<PublicacionResponseDto>> ObtenerTodasAsync()
    {
        var publicaciones = await _publicacionRepository.ObtenerTodasAsync();
        return publicaciones.Select(MapearADto).ToList();
    }

    public async Task<PublicacionResponseDto> ObtenerPorIdAsync(Guid id)
    {
        var publicacion = await _publicacionRepository.ObtenerPorIdAsync(id)
            ?? throw new NotFoundException($"No existe una publicacion con id '{id}'.");

        return MapearADto(publicacion);
    }

    public async Task<PublicacionResponseDto> CrearAsync(CrearPublicacionDto dto)
    {
        // --- Validaciones de reglas de negocio (mas alla de las anotaciones del DTO) ---
        var usuario = await _publicacionRepository.ObtenerUsuarioPorIdAsync(dto.UsuarioId)
            ?? throw new BusinessRuleException($"El usuario '{dto.UsuarioId}' no existe. No puede publicar.");

        var categoria = await _publicacionRepository.ObtenerCategoriaPorIdAsync(dto.CategoriaId)
            ?? throw new BusinessRuleException($"La categoria '{dto.CategoriaId}' no existe.");

        var publicacion = new Publicacion
        {
            Titulo = dto.Titulo.Trim(),
            Descripcion = dto.Descripcion.Trim(),
            CategoriaId = categoria.Id,
            FechaPublicacion = DateTime.UtcNow
        };

        // Se delega en el metodo de dominio de Usuario, tal como esta definido
        // en el Modelo C4 - Nivel 4 (Usuario.PublicarNuevoBien).
        usuario.PublicarNuevoBien(publicacion);

        await _publicacionRepository.AgregarAsync(publicacion);
        await _publicacionRepository.GuardarCambiosAsync();

        publicacion.Categoria = categoria;
        publicacion.Usuario = usuario;

        return MapearADto(publicacion);
    }

    public async Task<PublicacionResponseDto> CambiarEstadoAsync(Guid id, ActualizarEstadoPublicacionDto dto)
    {
        var publicacion = await _publicacionRepository.ObtenerPorIdAsync(id)
            ?? throw new NotFoundException($"No existe una publicacion con id '{id}'.");

        // La maquina de estados real vive en la entidad (Domain-Driven Design);
        // el servicio solo decide QUE metodo de dominio invocar segun la accion pedida.
        switch (dto.Accion.Trim().ToLowerInvariant())
        {
            case "en-negociacion":
                publicacion.MarcarEnNegociacion();
                break;
            case "confirmar":
                publicacion.ConfirmarIntercambio();
                break;
            case "cancelar":
                publicacion.Cancelar(dto.Motivo ?? string.Empty);
                break;
            default:
                throw new BusinessRuleException(
                    $"Accion '{dto.Accion}' no reconocida. Use: en-negociacion, confirmar o cancelar.");
        }

        await _publicacionRepository.GuardarCambiosAsync();
        return MapearADto(publicacion);
    }

    private static PublicacionResponseDto MapearADto(Publicacion p) => new(
        p.Id,
        p.Titulo,
        p.Descripcion,
        p.Estado,
        p.FechaPublicacion,
        p.CategoriaId,
        p.Categoria?.NombreCategoria,
        p.UsuarioId,
        p.Usuario?.NombreCompleto);
}
