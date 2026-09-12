using TruekeBogotaSolidario.Api.Common.Exceptions;
using TruekeBogotaSolidario.Api.Domain.Entities;
using TruekeBogotaSolidario.Api.DTOs.Usuarios;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;
using TruekeBogotaSolidario.Api.Services.Interfaces;

namespace TruekeBogotaSolidario.Api.Services;

// ==========================================================================
// CAPA DE SERVICIO / LOGICA DE NEGOCIO
// Equivalente al "UserService" del Modelo C4 - Nivel 3.
// ==========================================================================
public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _usuarioRepository;

    public UsuarioService(IUsuarioRepository usuarioRepository)
    {
        _usuarioRepository = usuarioRepository;
    }

    public async Task<List<UsuarioResponseDto>> ObtenerTodosAsync()
    {
        var usuarios = await _usuarioRepository.ObtenerTodosAsync();
        return usuarios.Select(MapearADto).ToList();
    }

    public async Task<UsuarioResponseDto> ObtenerPorIdAsync(Guid id)
    {
        var usuario = await _usuarioRepository.ObtenerPorIdAsync(id)
            ?? throw new NotFoundException($"No existe un usuario con id '{id}'.");

        return MapearADto(usuario);
    }

    public async Task<UsuarioResponseDto> CrearAsync(CrearUsuarioDto dto)
    {
        if (await _usuarioRepository.ExisteCorreoAsync(dto.Correo))
        {
            throw new BusinessRuleException($"Ya existe un usuario registrado con el correo '{dto.Correo}'.");
        }

        var usuario = new Usuario
        {
            NombreCompleto = dto.NombreCompleto.Trim(),
            Localidad = dto.Localidad.Trim(),
            Correo = dto.Correo.Trim().ToLowerInvariant()
        };

        await _usuarioRepository.AgregarAsync(usuario);
        await _usuarioRepository.GuardarCambiosAsync();

        return MapearADto(usuario);
    }

    private static UsuarioResponseDto MapearADto(Usuario u) =>
        new(u.Id, u.NombreCompleto, u.Localidad, u.Correo);
}
