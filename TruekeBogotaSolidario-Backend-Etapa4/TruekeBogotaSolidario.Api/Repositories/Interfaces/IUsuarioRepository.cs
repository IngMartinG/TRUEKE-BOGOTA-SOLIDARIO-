using TruekeBogotaSolidario.Api.Domain.Entities;

namespace TruekeBogotaSolidario.Api.Repositories.Interfaces;

public interface IUsuarioRepository
{
    Task<List<Usuario>> ObtenerTodosAsync();
    Task<Usuario?> ObtenerPorIdAsync(Guid id);
    Task<bool> ExisteCorreoAsync(string correo);
    Task AgregarAsync(Usuario usuario);
    Task GuardarCambiosAsync();
}
