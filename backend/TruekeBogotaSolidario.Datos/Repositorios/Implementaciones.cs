using Microsoft.EntityFrameworkCore;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Repositorios;

public sealed class UnidadDeTrabajo : IUnidadDeTrabajo
{
    private readonly TruekeDbContext _db;
    public UnidadDeTrabajo(TruekeDbContext db) => _db = db;

    public async Task GuardarCambiosAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictoDeConcurrenciaException("El recurso fue modificado por otra operación. Vuelve a intentarlo.", ex);
        }
        catch (DbUpdateException ex)
        {
            // p. ej. correo duplicado por registro simultáneo (índice único)
            throw new ConflictoDeConcurrenciaException("No se pudo guardar por un conflicto con otro registro.", ex);
        }
    }
}

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly TruekeDbContext _db;
    public UsuarioRepository(TruekeDbContext db) => _db = db;

    public Task<Usuario?> ObtenerPorIdAsync(Guid id) => _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id);
    public Task<Usuario?> ObtenerPorCorreoAsync(string correo) => _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);
    public Task<Usuario?> ObtenerPorGoogleSubAsync(string googleSub) => _db.Usuarios.FirstOrDefaultAsync(u => u.GoogleSub == googleSub);
    public Task<bool> ExisteCorreoAsync(string correo) => _db.Usuarios.AnyAsync(u => u.Correo == correo);
    public async Task<IReadOnlyList<Usuario>> ObtenerPorVerificacionAsync(EstadoVerificacion estado)
        => await _db.Usuarios.AsNoTracking().Where(u => u.EstadoVerificacion == estado).OrderBy(u => u.FechaRegistro).Take(200).ToListAsync();
    public Task<int> ContarPorRolAsync(RolUsuarioEnum rol) => _db.Usuarios.CountAsync(u => u.Rol == rol);
    public async Task<int?> ObtenerVersionSeguridadAsync(Guid id)
        => await _db.Usuarios.AsNoTracking().Where(u => u.Id == id).Select(u => (int?)u.VersionSeguridad).FirstOrDefaultAsync();
    public void Agregar(Usuario usuario) => _db.Usuarios.Add(usuario);
}

public sealed class CategoriaRepository : ICategoriaRepository
{
    private readonly TruekeDbContext _db;
    public CategoriaRepository(TruekeDbContext db) => _db = db;

    public Task<Categoria?> ObtenerPorIdAsync(int id) => _db.Categorias.FirstOrDefaultAsync(c => c.Id == id);
    public async Task<IReadOnlyList<Categoria>> ListarAsync() => await _db.Categorias.AsNoTracking().OrderBy(c => c.NombreCategoria).ToListAsync();
}

public sealed class PublicacionRepository : IPublicacionRepository
{
    private readonly TruekeDbContext _db;
    public PublicacionRepository(TruekeDbContext db) => _db = db;

    public Task<Publicacion?> ObtenerPorIdAsync(Guid id)
        => _db.Publicaciones.Include(p => p.Categoria).Include(p => p.Propietario).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<(IReadOnlyList<Publicacion> Items, int Total)> ListarVisiblesAsync(FiltroPublicaciones f, DateTime ahoraUtc)
    {
        var pagina = Math.Max(f.Pagina, 1);
        var tamano = Math.Clamp(f.Tamano, 1, 50);

        var q = _db.Publicaciones.AsNoTracking().Include(p => p.Categoria).Include(p => p.Propietario)
            .Where(p => p.Estado == EstadoPublicacionEnum.Disponible && !p.EstaOculta);

        if (f.CategoriaId.HasValue) q = q.Where(p => p.CategoriaId == f.CategoriaId.Value);
        if (f.Modo.HasValue) q = q.Where(p => p.Modo == f.Modo.Value);
        if (!string.IsNullOrWhiteSpace(f.Localidad)) { var loc = f.Localidad.Trim(); q = q.Where(p => p.Localidad == loc); }
        if (!string.IsNullOrWhiteSpace(f.Texto)) { var t = f.Texto.Trim(); q = q.Where(p => p.Titulo.Contains(t) || p.Descripcion.Contains(t)); }

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(p => p.DestacadaHasta != null && p.DestacadaHasta > ahoraUtc) // destacadas VIGENTES primero
            .ThenByDescending(p => p.FechaPublicacion)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<Publicacion>> ListarVisiblesEnAreaAsync(double minLat, double maxLat, double minLon, double maxLon,
        int? categoriaId, ModoTransaccion? modo, int maximo)
    {
        var q = _db.Publicaciones.AsNoTracking().Include(p => p.Categoria).Include(p => p.Propietario)
            .Where(p => p.Estado == EstadoPublicacionEnum.Disponible && !p.EstaOculta
                        && p.Latitud != null && p.Longitud != null
                        && p.Latitud >= minLat && p.Latitud <= maxLat && p.Longitud >= minLon && p.Longitud <= maxLon);
        if (categoriaId.HasValue) q = q.Where(p => p.CategoriaId == categoriaId.Value);
        if (modo.HasValue) q = q.Where(p => p.Modo == modo.Value);
        return await q.OrderByDescending(p => p.FechaPublicacion).Take(Math.Clamp(maximo, 1, 1000)).ToListAsync();
    }

    public async Task<IReadOnlyList<Publicacion>> ListarPorPropietarioAsync(Guid propietarioId)
        => await _db.Publicaciones.AsNoTracking().Include(p => p.Categoria).Include(p => p.Propietario)
            .Where(p => p.PropietarioId == propietarioId).OrderByDescending(p => p.FechaPublicacion).Take(200).ToListAsync();

    public Task<int> ContarActivasPorUsuarioAsync(Guid usuarioId)
        => _db.Publicaciones.CountAsync(p => p.PropietarioId == usuarioId
            && (p.Estado == EstadoPublicacionEnum.Disponible || p.Estado == EstadoPublicacionEnum.EnNegociacion));

    public void Agregar(Publicacion publicacion) => _db.Publicaciones.Add(publicacion);
}

public sealed class SolicitudRepository : ISolicitudRepository
{
    private readonly TruekeDbContext _db;
    public SolicitudRepository(TruekeDbContext db) => _db = db;

    public Task<Solicitud?> ObtenerPorIdAsync(Guid id)
        => _db.Solicitudes.Include(s => s.Publicacion).Include(s => s.Solicitante).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IReadOnlyList<Solicitud>> ListarPorSolicitanteAsync(Guid solicitanteId)
        => await _db.Solicitudes.AsNoTracking().Include(s => s.Publicacion).Include(s => s.Solicitante)
            .Where(s => s.SolicitanteId == solicitanteId).OrderByDescending(s => s.FechaSolicitud).Take(200).ToListAsync();

    public async Task<IReadOnlyList<Solicitud>> ListarRecibidasAsync(Guid propietarioId)
        => await _db.Solicitudes.AsNoTracking().Include(s => s.Publicacion).Include(s => s.Solicitante)
            .Where(s => s.Publicacion!.PropietarioId == propietarioId).OrderByDescending(s => s.FechaSolicitud).Take(200).ToListAsync();

    public Task<Solicitud?> ObtenerPendientePorPublicacionAsync(Guid publicacionId)
        => _db.Solicitudes.FirstOrDefaultAsync(s => s.PublicacionId == publicacionId && s.Estado == EstadoSolicitud.Pendiente);

    public Task<int> ContarPendientesPorSolicitanteAsync(Guid solicitanteId)
        => _db.Solicitudes.CountAsync(s => s.SolicitanteId == solicitanteId && s.Estado == EstadoSolicitud.Pendiente);

    public Task<bool> ExisteAceptadaAsync(Guid publicacionId, Guid solicitanteId)
        => _db.Solicitudes.AnyAsync(s => s.PublicacionId == publicacionId && s.SolicitanteId == solicitanteId && s.Estado == EstadoSolicitud.Aceptada);

    public void Agregar(Solicitud solicitud) => _db.Solicitudes.Add(solicitud);
}

public sealed class TransaccionRepository : ITransaccionRepository
{
    private readonly TruekeDbContext _db;
    public TransaccionRepository(TruekeDbContext db) => _db = db;

    public Task<int> ContarDesdeAsync(Guid usuarioId, DateTime desdeUtc)
        => _db.Transacciones.CountAsync(t => t.FechaUtc >= desdeUtc
            && ((t.OferenteId == usuarioId && t.PuntosOtorgadosOferente) || (t.ReceptorId == usuarioId && t.PuntosOtorgadosReceptor)));

    public async Task<IReadOnlyList<Transaccion>> ListarPorUsuarioAsync(Guid usuarioId)
        => await _db.Transacciones.AsNoTracking().Where(t => t.OferenteId == usuarioId || t.ReceptorId == usuarioId)
            .OrderByDescending(t => t.FechaUtc).Take(200).ToListAsync();

    public void Agregar(Transaccion transaccion) => _db.Transacciones.Add(transaccion);
}

public sealed class PagoRepository : IPagoRepository
{
    private readonly TruekeDbContext _db;
    public PagoRepository(TruekeDbContext db) => _db = db;

    public Task<Pago?> ObtenerPorReferenciaAsync(string referencia)
        => _db.Pagos.FirstOrDefaultAsync(p => p.Referencia == referencia);

    public Task<Pago?> ObtenerUltimoAprobadoAsync(Guid usuarioId, ConceptoPago concepto)
        => _db.Pagos.Where(p => p.UsuarioId == usuarioId && p.Concepto == concepto && p.Estado == EstadoPago.Aprobado)
            .OrderByDescending(p => p.FechaUtc).FirstOrDefaultAsync();

    public Task<int> ContarPendientesAsync(Guid usuarioId, DateTime desdeUtc)
        => _db.Pagos.CountAsync(p => p.UsuarioId == usuarioId && p.Estado == EstadoPago.Pendiente && p.FechaUtc >= desdeUtc);

    public Task<bool> ExistePendienteAsync(Guid usuarioId, ConceptoPago concepto, Guid? publicacionId)
        => _db.Pagos.AnyAsync(p => p.UsuarioId == usuarioId && p.Concepto == concepto && p.Estado == EstadoPago.Pendiente
            && (publicacionId == null || p.PublicacionId == publicacionId));

    public async Task<IReadOnlyList<Pago>> ListarPendientesAnterioresAAsync(DateTime limiteUtc, int maximo)
        => await _db.Pagos.Where(p => p.Estado == EstadoPago.Pendiente && p.FechaUtc < limiteUtc)
            .OrderBy(p => p.FechaUtc).Take(maximo).ToListAsync();

    public void Agregar(Pago pago) => _db.Pagos.Add(pago);
}

public sealed class SaludBaseDatos : ISaludBaseDatos
{
    private readonly TruekeDbContext _db;
    public SaludBaseDatos(TruekeDbContext db) => _db = db;
    public Task<bool> PuedeConectarAsync(CancellationToken ct) => _db.Database.CanConnectAsync(ct);
}

public sealed class SesionRefreshRepository : ISesionRefreshRepository
{
    private readonly TruekeDbContext _db;
    public SesionRefreshRepository(TruekeDbContext db) => _db = db;

    public Task<SesionRefresh?> ObtenerPorHashAsync(string tokenHash)
        => _db.SesionesRefresh.FirstOrDefaultAsync(s => s.TokenHash == tokenHash);

    public async Task RevocarFamiliaAsync(Guid familiaId, DateTime ahoraUtc)
    {
        foreach (var s in await _db.SesionesRefresh.Where(s => s.FamiliaId == familiaId && s.RevocadoUtc == null).ToListAsync())
            s.Revocar(ahoraUtc);
    }

    public async Task RevocarTodasDelUsuarioAsync(Guid usuarioId, DateTime ahoraUtc)
    {
        foreach (var s in await _db.SesionesRefresh.Where(s => s.UsuarioId == usuarioId && s.RevocadoUtc == null).ToListAsync())
            s.Revocar(ahoraUtc);
    }

    public async Task<int> PurgarAsync(DateTime antesDeUtc, int maximo)
    {
        var viejas = await _db.SesionesRefresh
            .Where(s => s.ExpiraUtc < antesDeUtc || (s.RevocadoUtc != null && s.RevocadoUtc < antesDeUtc))
            .OrderBy(s => s.ExpiraUtc).Take(maximo).ToListAsync();
        _db.SesionesRefresh.RemoveRange(viejas);
        await _db.SaveChangesAsync();
        return viejas.Count;
    }

    public void Agregar(SesionRefresh sesion) => _db.SesionesRefresh.Add(sesion);
}

public sealed class TokenUsoUnicoRepository : ITokenUsoUnicoRepository
{
    private readonly TruekeDbContext _db;
    public TokenUsoUnicoRepository(TruekeDbContext db) => _db = db;

    public Task<TokenUsoUnico?> ObtenerPorHashAsync(string tokenHash, PropositoToken proposito)
        => _db.TokensUsoUnico.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Proposito == proposito);

    public async Task InvalidarVigentesAsync(Guid usuarioId, PropositoToken proposito, DateTime ahoraUtc)
    {
        foreach (var t in await _db.TokensUsoUnico.Where(t => t.UsuarioId == usuarioId && t.Proposito == proposito && t.UsadoUtc == null).ToListAsync())
            t.Consumir(ahoraUtc);
    }

    public Task<int> ContarDesdeAsync(Guid usuarioId, PropositoToken proposito, DateTime desdeUtc)
        => _db.TokensUsoUnico.CountAsync(t => t.UsuarioId == usuarioId && t.Proposito == proposito && t.CreadoUtc >= desdeUtc);

    public async Task<int> PurgarAsync(DateTime antesDeUtc, int maximo)
    {
        var viejos = await _db.TokensUsoUnico.Where(t => t.ExpiraUtc < antesDeUtc).OrderBy(t => t.ExpiraUtc).Take(maximo).ToListAsync();
        _db.TokensUsoUnico.RemoveRange(viejos);
        await _db.SaveChangesAsync();
        return viejos.Count;
    }

    public void Agregar(TokenUsoUnico token) => _db.TokensUsoUnico.Add(token);
}

public sealed class ComentarioRepository : IComentarioRepository
{
    private readonly TruekeDbContext _db;
    public ComentarioRepository(TruekeDbContext db) => _db = db;

    public Task<Comentario?> ObtenerPorIdAsync(Guid id)
        => _db.Comentarios.Include(c => c.Autor).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(IReadOnlyList<Comentario> Items, int Total)> ListarPorPublicacionAsync(Guid publicacionId, bool incluirOcultos, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Comentarios.AsNoTracking().Include(c => c.Autor).Where(c => c.PublicacionId == publicacionId);
        if (!incluirOcultos) q = q.Where(c => !c.EstaOculto);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.FechaUtc).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public Task<int> ContarDelAutorDesdeAsync(Guid autorId, DateTime desdeUtc)
        => _db.Comentarios.CountAsync(c => c.AutorId == autorId && c.FechaUtc >= desdeUtc);

    public void Agregar(Comentario comentario) => _db.Comentarios.Add(comentario);
}

public sealed class AuditoriaRepository : IAuditoriaRepository
{
    private readonly TruekeDbContext _db;
    public AuditoriaRepository(TruekeDbContext db) => _db = db;
    public void Agregar(AuditoriaEvento evento) => _db.Auditoria.Add(evento);
}
