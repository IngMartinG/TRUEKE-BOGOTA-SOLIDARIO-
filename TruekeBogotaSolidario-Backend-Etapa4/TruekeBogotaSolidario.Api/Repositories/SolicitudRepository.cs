using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Api.Data;
using TruekeBogotaSolidario.Api.Domain.Entities;
using TruekeBogotaSolidario.Api.Repositories.Interfaces;

namespace TruekeBogotaSolidario.Api.Repositories;

// ==========================================================================
// CAPA DE DATOS (REPOSITORY) - Implementacion sobre Solicitud
// Corresponde al "MessageRepository" del Modelo C4 - Nivel 3, especializado
// aqui en solicitudes de trueque en vez de mensajes de chat.
// ==========================================================================
public class SolicitudRepository : ISolicitudRepository
{
    private readonly TruekeDbContext _context;

    public SolicitudRepository(TruekeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Solicitud>> ObtenerPorPublicacionAsync(Guid publicacionId)
    {
        return await _context.Solicitudes
            .Include(s => s.Usuario)
            .Include(s => s.Publicacion)
            .AsNoTracking()
            .Where(s => s.PublicacionId == publicacionId)
            .OrderByDescending(s => s.FechaSolicitud)
            .ToListAsync();
    }

    public async Task<Solicitud?> ObtenerPorIdAsync(Guid id)
    {
        return await _context.Solicitudes
            .Include(s => s.Usuario)
            .Include(s => s.Publicacion)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AgregarAsync(Solicitud solicitud)
    {
        await _context.Solicitudes.AddAsync(solicitud);
    }

    public async Task GuardarCambiosAsync()
    {
        await _context.SaveChangesAsync();
    }
}
