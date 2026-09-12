using TruekeBogotaSolidario.Api.Domain.Entities;

namespace TruekeBogotaSolidario.Api.Repositories.Interfaces;

// ==========================================================================
// CAPA DE DATOS (REPOSITORY) - Contrato
// Unico punto autorizado para hablar con la base de datos sobre Publicacion.
// ==========================================================================
public interface IPublicacionRepository
{
    Task<List<Publicacion>> ObtenerTodasAsync();
    Task<Publicacion?> ObtenerPorIdAsync(Guid id);
    Task<Categoria?> ObtenerCategoriaPorIdAsync(int categoriaId);
    Task<Usuario?> ObtenerUsuarioPorIdAsync(Guid usuarioId);
    Task AgregarAsync(Publicacion publicacion);
    Task GuardarCambiosAsync();
}
