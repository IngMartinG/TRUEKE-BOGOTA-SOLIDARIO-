using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Api.Data;
using TruekeBogotaSolidario.Api.Domain.Entities;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;

namespace TruekeBogotaSolidario.Api.Repositories;

// ==========================================================================
// CAPA DE DATOS (REPOSITORY) - Implementacion sobre Usuario
// ==========================================================================
public class UsuarioRepository : IUsuarioRepository
{
    private readonly TruekeDbContext _context;

    public UsuarioRepository(TruekeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Usuario>> ObtenerTodosAsync()
    {
        return await _context.Usuarios.AsNoTracking().ToListAsync();
    }

    public async Task<Usuario?> ObtenerPorIdAsync(Guid id)
    {
        return await _context.Usuarios.FindAsync(id);
    }

    public async Task<bool> ExisteCorreoAsync(string correo)
    {
        return await _context.Usuarios.AnyAsync(u => u.Correo.ToLower() == correo.ToLower());
    }

    public async Task AgregarAsync(Usuario usuario)
    {
        await _context.Usuarios.AddAsync(usuario);
    }

    public async Task GuardarCambiosAsync()
    {
        await _context.SaveChangesAsync();
    }
}
