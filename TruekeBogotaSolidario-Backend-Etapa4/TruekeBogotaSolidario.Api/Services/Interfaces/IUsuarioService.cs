using TruekeBogotaSolidario.Api.DTOs.Usuarios;

namespace TruekeBogotaSolidario.Api.Services.Interfaces;

public interface IUsuarioService
{
    Task<List<UsuarioResponseDto>> ObtenerTodosAsync();
    Task<UsuarioResponseDto> ObtenerPorIdAsync(Guid id);
    Task<UsuarioResponseDto> CrearAsync(CrearUsuarioDto dto);
}
