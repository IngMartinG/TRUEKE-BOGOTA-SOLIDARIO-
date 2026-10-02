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

    public async Task<(IReadOnlyList<Usuario> Items, int Total)> BuscarAsync(string? texto, bool soloSuspendidos, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Usuarios.AsNoTracking().Where(u => !u.EstaEliminado);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var t = texto.Trim().ToLowerInvariant();
            q = q.Where(u => u.Correo.Contains(t) || u.NombreCompleto.ToLower().Contains(t)); // el correo ya se guarda en minúsculas
        }
        if (soloSuspendidos) q = q.Where(u => u.EstaSuspendido);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(u => u.FechaRegistro).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }
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

    /// <summary>Disponibles, no ocultas y cuyo dueño no está suspendido ni eliminado.</summary>
    private IQueryable<Publicacion> Visibles(DateTime ahoraUtc)
        => _db.Publicaciones.AsNoTracking().Include(p => p.Categoria).Include(p => p.Propietario)
            .Where(p => p.Estado == EstadoPublicacionEnum.Disponible && !p.EstaOculta && !p.Propietario!.EstaEliminado
                        && !(p.Propietario.EstaSuspendido && (p.Propietario.SuspendidoHasta == null || p.Propietario.SuspendidoHasta > ahoraUtc)));

    public async Task<(IReadOnlyList<Publicacion> Items, int Total)> ListarVisiblesAsync(FiltroPublicaciones f, DateTime ahoraUtc)
    {
        var pagina = Math.Max(f.Pagina, 1);
        var tamano = Math.Clamp(f.Tamano, 1, 50);

        var q = Visibles(ahoraUtc);
        if (f.CategoriaId.HasValue) q = q.Where(p => p.CategoriaId == f.CategoriaId.Value);
        if (f.Modo.HasValue) q = q.Where(p => p.Modo == f.Modo.Value);
        if (!string.IsNullOrWhiteSpace(f.Localidad)) { var loc = f.Localidad.Trim(); q = q.Where(p => p.Localidad == loc); }
        if (!string.IsNullOrWhiteSpace(f.Texto)) { var t = f.Texto.Trim(); q = q.Where(p => p.Titulo.Contains(t) || p.Descripcion.Contains(t)); }
        if (f.PrecioMin.HasValue) q = q.Where(p => p.PrecioReferenciaCop >= f.PrecioMin.Value);
        if (f.PrecioMax.HasValue) q = q.Where(p => p.PrecioReferenciaCop <= f.PrecioMax.Value);
        if (f.SoloVerificados) q = q.Where(p => p.Propietario!.EstadoVerificacion == EstadoVerificacion.Aprobada);

        var total = await q.CountAsync();
        var ordenada = f.Orden switch
        {
            // sin precio (Trueke/Donación) al final en ambos sentidos
            OrdenPublicaciones.PrecioAsc => q.OrderBy(p => p.PrecioReferenciaCop == null).ThenBy(p => p.PrecioReferenciaCop).ThenByDescending(p => p.FechaPublicacion),
            OrdenPublicaciones.PrecioDesc => q.OrderBy(p => p.PrecioReferenciaCop == null).ThenByDescending(p => p.PrecioReferenciaCop).ThenByDescending(p => p.FechaPublicacion),
            _ => q.OrderByDescending(p => p.DestacadaHasta != null && p.DestacadaHasta > ahoraUtc) // destacadas VIGENTES primero
                  .ThenByDescending(p => p.FechaPublicacion)
        };
        var items = await ordenada.ThenBy(p => p.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public async Task<(IReadOnlyList<Publicacion> Items, int Total)> ListarVisiblesDePropietarioAsync(Guid propietarioId, int pagina, int tamano, DateTime ahoraUtc)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = Visibles(ahoraUtc).Where(p => p.PropietarioId == propietarioId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(p => p.FechaPublicacion).ThenBy(p => p.Id).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<Publicacion>> ListarPorIdsAsync(IReadOnlyCollection<Guid> ids)
        => await _db.Publicaciones.AsNoTracking().Include(p => p.Categoria).Include(p => p.Propietario)
            .Where(p => ids.Contains(p.Id)).ToListAsync();

    public async Task<IReadOnlyList<Publicacion>> ListarVisiblesEnAreaAsync(double minLat, double maxLat, double minLon, double maxLon,
        int? categoriaId, ModoTransaccion? modo, int maximo, DateTime ahoraUtc)
    {
        var q = Visibles(ahoraUtc)
            .Where(p => p.Latitud != null && p.Longitud != null
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

    public async Task<IReadOnlyList<Publicacion>> ListarActivasParaActualizarAsync(Guid propietarioId)
        => await _db.Publicaciones.Where(p => p.PropietarioId == propietarioId
            && (p.Estado == EstadoPublicacionEnum.Disponible || p.Estado == EstadoPublicacionEnum.EnNegociacion)).ToListAsync();

    public void Agregar(Publicacion publicacion) => _db.Publicaciones.Add(publicacion);
}

public sealed class SolicitudRepository : ISolicitudRepository
{
    private readonly TruekeDbContext _db;
    public SolicitudRepository(TruekeDbContext db) => _db = db;

    public Task<Solicitud?> ObtenerPorIdAsync(Guid id)
        => _db.Solicitudes.Include(s => s.Publicacion).ThenInclude(p => p!.Propietario).Include(s => s.Solicitante).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IReadOnlyList<Solicitud>> ListarPorSolicitanteAsync(Guid solicitanteId)
        => await _db.Solicitudes.AsNoTracking().Include(s => s.Publicacion).ThenInclude(p => p!.Propietario).Include(s => s.Solicitante)
            .Where(s => s.SolicitanteId == solicitanteId).OrderByDescending(s => s.FechaSolicitud).Take(200).ToListAsync();

    public async Task<IReadOnlyList<Solicitud>> ListarRecibidasAsync(Guid propietarioId)
        => await _db.Solicitudes.AsNoTracking().Include(s => s.Publicacion).ThenInclude(p => p!.Propietario).Include(s => s.Solicitante)
            .Where(s => s.Publicacion!.PropietarioId == propietarioId).OrderByDescending(s => s.FechaSolicitud).Take(200).ToListAsync();

    public Task<Solicitud?> ObtenerPendientePorPublicacionAsync(Guid publicacionId)
        => _db.Solicitudes.FirstOrDefaultAsync(s => s.PublicacionId == publicacionId && s.Estado == EstadoSolicitud.Pendiente);

    public Task<int> ContarPendientesPorSolicitanteAsync(Guid solicitanteId)
        => _db.Solicitudes.CountAsync(s => s.SolicitanteId == solicitanteId && s.Estado == EstadoSolicitud.Pendiente);

    public Task<bool> ExisteAceptadaAsync(Guid publicacionId, Guid solicitanteId)
        => _db.Solicitudes.AnyAsync(s => s.PublicacionId == publicacionId && s.SolicitanteId == solicitanteId
            && (s.Estado == EstadoSolicitud.Aceptada || s.Estado == EstadoSolicitud.Completada));

    public Task<Solicitud?> ObtenerEnCursoPorPublicacionAsync(Guid publicacionId)
        => _db.Solicitudes.FirstOrDefaultAsync(s => s.PublicacionId == publicacionId
            && (s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aceptada));

    public async Task<IReadOnlyList<Guid>> ListarParaCierreAutomaticoAsync(DateTime limiteConConfirmacion, DateTime limiteSinConfirmacion, int maximo)
        => await _db.Solicitudes.AsNoTracking()
            .Where(s => s.Estado == EstadoSolicitud.Aceptada && s.FechaAceptacionUtc != null
                && (((s.ConfirmadaPorDuenioUtc != null || s.ConfirmadaPorSolicitanteUtc != null) && s.FechaAceptacionUtc < limiteConConfirmacion)
                    || (s.ConfirmadaPorDuenioUtc == null && s.ConfirmadaPorSolicitanteUtc == null && s.FechaAceptacionUtc < limiteSinConfirmacion)))
            .OrderBy(s => s.FechaAceptacionUtc).Take(maximo).Select(s => s.Id).ToListAsync();

    public async Task<IReadOnlyList<Solicitud>> ListarPendientesDelUsuarioAsync(Guid usuarioId)
        => await _db.Solicitudes.Include(s => s.Publicacion)
            .Where(s => (s.Estado == EstadoSolicitud.Pendiente || s.Estado == EstadoSolicitud.Aceptada)
                        && (s.SolicitanteId == usuarioId || s.Publicacion!.PropietarioId == usuarioId))
            .ToListAsync();

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

    public async Task<(IReadOnlyList<Pago> Items, int Total)> ListarPorEstadoAsync(EstadoPago estado, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Pagos.AsNoTracking().Where(p => p.Estado == estado);
        var total = await q.CountAsync();
        var items = await q.OrderBy(p => p.FechaUtc).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<Pago>> ListarPorUsuarioAsync(Guid usuarioId, int maximo)
        => await _db.Pagos.AsNoTracking().Where(p => p.UsuarioId == usuarioId).OrderByDescending(p => p.FechaUtc).Take(maximo).ToListAsync();

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

public sealed class NotificacionRepository : INotificacionRepository
{
    private readonly TruekeDbContext _db;
    public NotificacionRepository(TruekeDbContext db) => _db = db;

    public Task<Notificacion?> ObtenerAsync(Guid id, Guid usuarioId)
        => _db.Notificaciones.FirstOrDefaultAsync(n => n.Id == id && n.UsuarioId == usuarioId);

    public async Task<(IReadOnlyList<Notificacion> Items, int Total)> ListarAsync(Guid usuarioId, bool soloNoLeidas, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuarioId);
        if (soloNoLeidas) q = q.Where(n => n.LeidaUtc == null);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(n => n.FechaUtc).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<Notificacion>> ListarTodasAsync(Guid usuarioId, int maximo)
        => await _db.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuarioId).OrderByDescending(n => n.FechaUtc).Take(maximo).ToListAsync();

    public Task<int> ContarNoLeidasAsync(Guid usuarioId)
        => _db.Notificaciones.CountAsync(n => n.UsuarioId == usuarioId && n.LeidaUtc == null);

    public async Task MarcarTodasLeidasAsync(Guid usuarioId, DateTime ahoraUtc)
    {
        foreach (var n in await _db.Notificaciones.Where(n => n.UsuarioId == usuarioId && n.LeidaUtc == null).Take(1000).ToListAsync())
            n.MarcarLeida(ahoraUtc);
    }

    public async Task<int> PurgarLeidasAsync(DateTime leidasAntesDeUtc, int maximo)
    {
        var viejas = await _db.Notificaciones.Where(n => n.LeidaUtc != null && n.LeidaUtc < leidasAntesDeUtc).Take(maximo).ToListAsync();
        _db.Notificaciones.RemoveRange(viejas);
        await _db.SaveChangesAsync();
        return viejas.Count;
    }

    public void Agregar(Notificacion notificacion) => _db.Notificaciones.Add(notificacion);
}

public sealed class ConversacionRepository : IConversacionRepository
{
    private readonly TruekeDbContext _db;
    public ConversacionRepository(TruekeDbContext db) => _db = db;

    private IQueryable<Conversacion> ConDetalle() => _db.Conversaciones
        .Include(c => c.Solicitud).ThenInclude(s => s!.Publicacion)
        .Include(c => c.Duenio).Include(c => c.Solicitante);

    public Task<Conversacion?> ObtenerAsync(Guid id) => ConDetalle().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyDictionary<Guid, Guid>> IdsPorSolicitudAsync(IReadOnlyCollection<Guid> solicitudIds)
        => await _db.Conversaciones.AsNoTracking().Where(c => solicitudIds.Contains(c.SolicitudId))
            .ToDictionaryAsync(c => c.SolicitudId, c => c.Id);

    public async Task<IReadOnlyList<Conversacion>> ListarDeUsuarioAsync(Guid usuarioId, int maximo)
        => await ConDetalle().AsNoTracking().Where(c => c.DuenioId == usuarioId || c.SolicitanteId == usuarioId)
            .OrderByDescending(c => c.UltimoMensajeUtc).Take(Math.Clamp(maximo, 1, 200)).ToListAsync();

    public async Task<IReadOnlyDictionary<Guid, int>> ContarNoLeidosAsync(Guid usuarioId, IReadOnlyCollection<Guid> conversacionIds)
        => await _db.Mensajes.AsNoTracking()
            .Where(m => conversacionIds.Contains(m.ConversacionId) && m.AutorId != usuarioId && m.LeidoUtc == null)
            .GroupBy(m => m.ConversacionId).Select(g => new { g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Total);

    public async Task<IReadOnlyDictionary<Guid, Mensaje>> UltimosMensajesAsync(IReadOnlyCollection<Guid> conversacionIds)
    {
        var ultimos = await _db.Mensajes.AsNoTracking().Where(m => conversacionIds.Contains(m.ConversacionId))
            .GroupBy(m => m.ConversacionId)
            .Select(g => g.OrderByDescending(m => m.FechaUtc).First())
            .ToListAsync();
        return ultimos.ToDictionary(m => m.ConversacionId);
    }

    public async Task<IReadOnlyList<Mensaje>> ListarMensajesAsync(Guid conversacionId, DateTime? antesDeUtc, int tamano)
    {
        var q = _db.Mensajes.AsNoTracking().Where(m => m.ConversacionId == conversacionId);
        if (antesDeUtc.HasValue) q = q.Where(m => m.FechaUtc < antesDeUtc.Value);
        return await q.OrderByDescending(m => m.FechaUtc).Take(Math.Clamp(tamano, 1, 100)).ToListAsync();
    }

    public async Task MarcarLeidosAsync(Guid conversacionId, Guid lectorId, DateTime ahoraUtc)
    {
        foreach (var m in await _db.Mensajes.Where(m => m.ConversacionId == conversacionId && m.AutorId != lectorId && m.LeidoUtc == null).Take(1000).ToListAsync())
            m.MarcarLeido(ahoraUtc);
    }

    public Task<Mensaje?> ObtenerMensajeAsync(Guid id) => _db.Mensajes.Include(m => m.Conversacion).FirstOrDefaultAsync(m => m.Id == id);

    public Task<int> ContarMensajesDelAutorDesdeAsync(Guid autorId, DateTime desdeUtc)
        => _db.Mensajes.CountAsync(m => m.AutorId == autorId && m.FechaUtc >= desdeUtc);

    public async Task<IReadOnlyList<Mensaje>> ListarMensajesDelAutorAsync(Guid autorId, int maximo)
        => await _db.Mensajes.AsNoTracking().Where(m => m.AutorId == autorId).OrderByDescending(m => m.FechaUtc).Take(maximo).ToListAsync();

    public void Agregar(Conversacion conversacion) => _db.Conversaciones.Add(conversacion);
    public void AgregarMensaje(Mensaje mensaje) => _db.Mensajes.Add(mensaje);
}

public sealed class DenunciaRepository : IDenunciaRepository
{
    private readonly TruekeDbContext _db;
    public DenunciaRepository(TruekeDbContext db) => _db = db;

    public Task<Denuncia?> ObtenerAsync(Guid id) => _db.Denuncias.FirstOrDefaultAsync(d => d.Id == id);

    public Task<bool> ExisteAsync(Guid denuncianteId, TipoObjetoDenuncia tipo, Guid objetivoId)
        => _db.Denuncias.AnyAsync(d => d.DenuncianteId == denuncianteId && d.Tipo == tipo && d.ObjetivoId == objetivoId);

    public Task<int> ContarDelDenuncianteDesdeAsync(Guid denuncianteId, DateTime desdeUtc)
        => _db.Denuncias.CountAsync(d => d.DenuncianteId == denuncianteId && d.FechaUtc >= desdeUtc);

    public async Task<IReadOnlyList<Denuncia>> ListarPorEstadoAsync(EstadoDenuncia estado, int maximo)
        => await _db.Denuncias.AsNoTracking().Where(d => d.Estado == estado).OrderBy(d => d.FechaUtc).Take(Math.Clamp(maximo, 1, 1000)).ToListAsync();

    public async Task<IReadOnlyList<Denuncia>> PendientesDelObjetivoAsync(TipoObjetoDenuncia tipo, Guid objetivoId)
        => await _db.Denuncias.Where(d => d.Tipo == tipo && d.ObjetivoId == objetivoId && d.Estado == EstadoDenuncia.Pendiente).ToListAsync();

    public async Task<IReadOnlyList<Denuncia>> ListarDelDenuncianteAsync(Guid denuncianteId, int maximo)
        => await _db.Denuncias.AsNoTracking().Where(d => d.DenuncianteId == denuncianteId).OrderByDescending(d => d.FechaUtc).Take(maximo).ToListAsync();

    public void Agregar(Denuncia denuncia) => _db.Denuncias.Add(denuncia);
}

public sealed class FavoritoRepository : IFavoritoRepository
{
    private readonly TruekeDbContext _db;
    public FavoritoRepository(TruekeDbContext db) => _db = db;

    public Task<Favorito?> ObtenerAsync(Guid usuarioId, Guid publicacionId)
        => _db.Favoritos.FirstOrDefaultAsync(f => f.UsuarioId == usuarioId && f.PublicacionId == publicacionId);

    public async Task<IReadOnlySet<Guid>> FiltrarFavoritasAsync(Guid usuarioId, IReadOnlyCollection<Guid> publicacionIds)
        => (await _db.Favoritos.AsNoTracking().Where(f => f.UsuarioId == usuarioId && publicacionIds.Contains(f.PublicacionId))
            .Select(f => f.PublicacionId).ToListAsync()).ToHashSet();

    public async Task<(IReadOnlyList<Guid> Ids, int Total)> ListarAsync(Guid usuarioId, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Favoritos.AsNoTracking().Where(f => f.UsuarioId == usuarioId);
        var total = await q.CountAsync();
        var ids = await q.OrderByDescending(f => f.FechaUtc).Skip((pagina - 1) * tamano).Take(tamano).Select(f => f.PublicacionId).ToListAsync();
        return (ids, total);
    }

    public Task<int> ContarAsync(Guid usuarioId) => _db.Favoritos.CountAsync(f => f.UsuarioId == usuarioId);
    public void Agregar(Favorito favorito) => _db.Favoritos.Add(favorito);
    public void Quitar(Favorito favorito) => _db.Favoritos.Remove(favorito);
}

public sealed class CalificacionRepository : ICalificacionRepository
{
    private readonly TruekeDbContext _db;
    public CalificacionRepository(TruekeDbContext db) => _db = db;

    public Task<Calificacion?> ObtenerAsync(Guid id) => _db.Calificaciones.Include(c => c.Autor).FirstOrDefaultAsync(c => c.Id == id);

    public Task<bool> ExisteAsync(Guid solicitudId, Guid autorId)
        => _db.Calificaciones.AnyAsync(c => c.SolicitudId == solicitudId && c.AutorId == autorId);

    public async Task<IReadOnlySet<Guid>> SolicitudesCalificadasPorAsync(Guid autorId, IReadOnlyCollection<Guid> solicitudIds)
        => (await _db.Calificaciones.AsNoTracking().Where(c => c.AutorId == autorId && solicitudIds.Contains(c.SolicitudId))
            .Select(c => c.SolicitudId).ToListAsync()).ToHashSet();

    public async Task<(IReadOnlyList<Calificacion> Items, int Total)> ListarRecibidasAsync(Guid calificadoId, int pagina, int tamano)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Calificaciones.AsNoTracking().Include(c => c.Autor).Where(c => c.CalificadoId == calificadoId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.FechaUtc).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public async Task<IReadOnlyList<Calificacion>> ListarDelAutorAsync(Guid autorId, int maximo)
        => await _db.Calificaciones.AsNoTracking().Where(c => c.AutorId == autorId).OrderByDescending(c => c.FechaUtc).Take(maximo).ToListAsync();

    public void Agregar(Calificacion calificacion) => _db.Calificaciones.Add(calificacion);
}

public sealed class ComentarioRepository : IComentarioRepository
{
    private readonly TruekeDbContext _db;
    public ComentarioRepository(TruekeDbContext db) => _db = db;

    public Task<Comentario?> ObtenerPorIdAsync(Guid id)
        => _db.Comentarios.Include(c => c.Autor).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(IReadOnlyList<Comentario> Items, int Total)> ListarPorPublicacionAsync(Guid publicacionId, bool incluirOcultos, int pagina, int tamano, DateTime ahoraUtc)
    {
        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, 50);
        var q = _db.Comentarios.AsNoTracking().Include(c => c.Autor).Where(c => c.PublicacionId == publicacionId);
        if (!incluirOcultos)
            q = q.Where(c => !c.EstaOculto
                && !(c.Autor!.EstaSuspendido && (c.Autor.SuspendidoHasta == null || c.Autor.SuspendidoHasta > ahoraUtc)));
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.FechaUtc).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync();
        return (items, total);
    }

    public Task<int> ContarDelAutorDesdeAsync(Guid autorId, DateTime desdeUtc)
        => _db.Comentarios.CountAsync(c => c.AutorId == autorId && c.FechaUtc >= desdeUtc);

    public async Task<IReadOnlyList<Comentario>> ListarDelAutorAsync(Guid autorId, int maximo)
        => await _db.Comentarios.Where(c => c.AutorId == autorId).OrderByDescending(c => c.FechaUtc).Take(maximo).ToListAsync();

    public void Agregar(Comentario comentario) => _db.Comentarios.Add(comentario);
}

public sealed class AuditoriaRepository : IAuditoriaRepository
{
    private readonly TruekeDbContext _db;
    public AuditoriaRepository(TruekeDbContext db) => _db = db;
    public void Agregar(AuditoriaEvento evento) => _db.Auditoria.Add(evento);
}
