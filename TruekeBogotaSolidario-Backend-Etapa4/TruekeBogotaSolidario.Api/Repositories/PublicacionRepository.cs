using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Api.Data;
using TruekeBogotaSolidario.Api.Domain.Entities;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;

namespace TruekeBogotaSolidario.Api.Repositories;

// ==========================================================================
// CAPA DE DATOS (REPOSITORY) - Implementacion
// Unica clase que ejecuta consultas EF Core / SQL sobre Publicacion.
// No contiene reglas de negocio: eso pertenece a la capa de Servicio.
// ==========================================================================
public class PublicacionRepository : IPublicacionRepository
{
    private readonly TruekeDbContext _context;

    public PublicacionRepository(TruekeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Publicacion>> ObtenerTodasAsync()
    {
        return await _context.Publicaciones
            .Include(p => p.Categoria)
            .Include(p => p.Usuario)
            .AsNoTracking()
            .OrderByDescending(p => p.FechaPublicacion)
            .ToListAsync();
    }

    public async Task<Publicacion?> ObtenerPorIdAsync(Guid id)
    {
        return await _context.Publicaciones
            .Include(p => p.Categoria)
            .Include(p => p.Usuario)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Categoria?> ObtenerCategoriaPorIdAsync(int categoriaId)
    {
        return await _context.Categorias.FindAsync(categoriaId);
    }

    public async Task<Usuario?> ObtenerUsuarioPorIdAsync(Guid usuarioId)
    {
        return await _context.Usuarios.FindAsync(usuarioId);
    }

    public async Task AgregarAsync(Publicacion publicacion)
    {
        await _context.Publicaciones.AddAsync(publicacion);
    }

    public async Task GuardarCambiosAsync()
    {
        await _context.SaveChangesAsync();
    }
}
