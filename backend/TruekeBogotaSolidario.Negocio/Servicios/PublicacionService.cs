using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IPublicacionService
{
    Task<PublicacionDto> CrearAsync(Guid actorId, CrearPublicacionRequest r);
    /// <summary>Solo el dueño y solo si está Disponible. Reemplaza todos los campos (incluida la lista de fotos).</summary>
    Task<PublicacionDto> EditarAsync(Guid actorId, Guid id, CrearPublicacionRequest r);
    Task<PaginaDto<PublicacionDto>> ListarAsync(Guid? actorId, FiltroPublicacionesRequest f);
    Task<PublicacionDto> ObtenerAsync(Guid? actorId, Guid id);
    Task<IReadOnlyList<PublicacionCercanaDto>> ListarCercanasAsync(Guid? actorId, CercanasRequest r);
    Task<IReadOnlyList<PublicacionDto>> ListarMiasAsync(Guid actorId);
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync();
    Task CancelarAsync(Guid actorId, Guid id, string? motivo);
    /// <summary>Perfil público; usuarios eliminados o suspendidos "no existen" (404).</summary>
    Task<PerfilUsuarioDto> ObtenerPerfilAsync(Guid usuarioId);
    Task<PaginaDto<PublicacionDto>> ListarDePerfilAsync(Guid? actorId, Guid usuarioId, int pagina, int tamano);
    Task MarcarFavoritaAsync(Guid actorId, Guid publicacionId);
    Task QuitarFavoritaAsync(Guid actorId, Guid publicacionId);
    Task<PaginaDto<PublicacionDto>> ListarFavoritasAsync(Guid actorId, int pagina, int tamano);
}

public sealed class PublicacionService : IPublicacionService
{
    public const int MaxFavoritos = 500;
    private const int MaxCandidatosCercanas = 500;

    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICategoriaRepository _categorias;
    private readonly ISolicitudRepository _solicitudes;
    private readonly IFavoritoRepository _favoritos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;
    private readonly IAlmacenArchivos _almacen;

    public PublicacionService(IPublicacionRepository pubs, IUsuarioRepository usuarios, ICategoriaRepository categorias,
        ISolicitudRepository solicitudes, IFavoritoRepository favoritos, IUnidadDeTrabajo uow, IOptions<UrlsOpciones> urls,
        TimeProvider reloj, INotificador notificador, IAlmacenArchivos almacen)
    {
        _pubs = pubs; _usuarios = usuarios; _categorias = categorias; _solicitudes = solicitudes; _favoritos = favoritos; _uow = uow;
        _urls = urls.Value; _reloj = reloj; _notificador = notificador; _almacen = almacen;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    /// <summary>Cada foto NUEVA debe ser un archivo propio subido con SAS (o, sin almacenamiento, de un host permitido).</summary>
    private async Task ValidarImagenesAsync(Guid actorId, IReadOnlyList<string> imagenes, IEnumerable<string> yaAceptadas)
    {
        if (imagenes.Count > Publicacion.MaxImagenes) throw new ReglaDeNegocioException($"Puedes subir como máximo {Publicacion.MaxImagenes} fotos.");
        var existentes = yaAceptadas.ToHashSet(StringComparer.Ordinal);
        foreach (var url in imagenes.Where(u => !existentes.Contains(u)))
        {
            if (_almacen.Habilitado) await _almacen.ValidarArchivoPropioAsync(url, actorId, TipoArchivoDto.Imagen);
            else ValidadorUrls.ExigirHostPermitido(url, _urls.HostsPermitidosImagenes, "La imagen");
        }
    }

    private static bool SuspendidoOEliminado(Usuario u, DateTime ahora) => u.EstaEliminado || u.SuspensionVigente(ahora);

    private async Task<IReadOnlySet<Guid>> FavoritasAsync(Guid? actorId, IEnumerable<Publicacion> pubs)
        => actorId.HasValue
            ? await _favoritos.FiltrarFavoritasAsync(actorId.Value, pubs.Select(p => p.Id).ToList())
            : new HashSet<Guid>();

    public async Task<PublicacionDto> CrearAsync(Guid actorId, CrearPublicacionRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        if (await _pubs.ContarActivasPorUsuarioAsync(actorId) >= Limites.MaxPublicacionesActivasPorUsuario)
            throw new ReglaDeNegocioException($"Alcanzaste el máximo de {Limites.MaxPublicacionesActivasPorUsuario} publicaciones activas.");

        var categoria = await _categorias.ObtenerPorIdAsync(r.CategoriaId) ?? throw new ReglaDeNegocioException("La categoría no existe.");
        var imagenes = r.Imagenes ?? new List<string>();
        await ValidarImagenesAsync(actorId, imagenes, Array.Empty<string>());

        var pub = new Publicacion(actorId, r.Titulo, r.Descripcion, categoria, (ModoTransaccion)(int)r.Modo, r.Localidad,
            r.PrecioReferenciaCop, r.Latitud, r.Longitud, imagenes);
        _pubs.Agregar(pub);
        await _uow.GuardarCambiosAsync();

        var creada = await _pubs.ObtenerPorIdAsync(pub.Id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        return Mapeos.APublicacionDto(creada, actorId, veExacto: true, veModeracion: false, Ahora);
    }

    public async Task<PublicacionDto> EditarAsync(Guid actorId, Guid id, CrearPublicacionRequest r)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);

        var categoria = await _categorias.ObtenerPorIdAsync(r.CategoriaId) ?? throw new ReglaDeNegocioException("La categoría no existe.");
        var imagenes = r.Imagenes ?? new List<string>();
        await ValidarImagenesAsync(actorId, imagenes, p.Imagenes.Select(i => i.Url)); // las fotos que ya tenía no se revalidan
        var ahora = Ahora;
        p.Editar(r.Titulo, r.Descripcion, categoria, (ModoTransaccion)(int)r.Modo, r.Localidad, r.PrecioReferenciaCop,
            r.Latitud, r.Longitud, imagenes, ahora);
        await _uow.GuardarCambiosAsync(); // RowVersion: si justo llegó una solicitud, 409
        var favorita = (await _favoritos.ObtenerAsync(actorId, p.Id)) is not null;
        return Mapeos.APublicacionDto(p, actorId, veExacto: true, veModeracion: false, ahora, favorita);
    }

    public async Task<PaginaDto<PublicacionDto>> ListarAsync(Guid? actorId, FiltroPublicacionesRequest f)
    {
        if (f.PrecioMin.HasValue && f.PrecioMax.HasValue && f.PrecioMin > f.PrecioMax)
            throw new ReglaDeNegocioException("El precio mínimo no puede ser mayor que el máximo.");
        var ahora = Ahora;
        var filtro = new FiltroPublicaciones
        {
            CategoriaId = f.CategoriaId,
            Modo = f.Modo.HasValue ? (ModoTransaccion)(int)f.Modo.Value : null,
            Localidad = f.Localidad,
            Texto = f.Texto,
            PrecioMin = f.PrecioMin,
            PrecioMax = f.PrecioMax,
            SoloVerificados = f.SoloVerificados,
            Orden = (OrdenPublicaciones)(int)f.Orden,
            Pagina = f.Pagina,
            Tamano = f.Tamano
        };
        var (items, total) = await _pubs.ListarVisiblesAsync(filtro, ahora);
        var favoritas = await FavoritasAsync(actorId, items);
        var dtos = items.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: actorId.HasValue && p.PropietarioId == actorId,
            veModeracion: false, ahora, favoritas.Contains(p.Id))).ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(f.Pagina, 1), Math.Clamp(f.Tamano, 1, 50));
    }

    public async Task<PublicacionDto> ObtenerAsync(Guid? actorId, Guid id)
    {
        var p = await _pubs.ObtenerPorIdAsync(id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        var actor = actorId.HasValue ? await _usuarios.ObtenerPorIdAsync(actorId.Value) : null;
        var ahora = Ahora;

        var esDueno = actor is not null && p.PropietarioId == actor.Id;
        var esModerador = actor is not null && actor.Rol >= RolUsuarioEnum.Administrador;
        var aceptada = actor is not null && !esDueno && await _solicitudes.ExisteAceptadaAsync(p.Id, actor.Id);
        var visiblePublicamente = p.Estado == EstadoPublicacionEnum.Disponible && !p.EstaOculta && !SuspendidoOEliminado(p.Propietario!, ahora);

        // Lo que no puede ver, "no existe" para el usuario (no se filtra información por códigos distintos)
        if (!visiblePublicamente && !esDueno && !esModerador && !aceptada)
            throw new NoEncontradoException("Publicación no encontrada.");

        var favorita = actor is not null && (await _favoritos.ObtenerAsync(actor.Id, p.Id)) is not null;
        return Mapeos.APublicacionDto(p, actor?.Id, veExacto: esDueno || esModerador || aceptada, veModeracion: esModerador, ahora, favorita);
    }

    /// <summary>
    /// El filtro por radio y la distancia usan las coordenadas que el usuario PUEDE ver (aproximadas si no es el dueño).
    /// Si se usaran las exactas, variando el punto de consulta se podría triangular la ubicación real.
    /// </summary>
    public async Task<IReadOnlyList<PublicacionCercanaDto>> ListarCercanasAsync(Guid? actorId, CercanasRequest r)
    {
        if (r.Lat is null || r.Lon is null) throw new ReglaDeNegocioException("Indica latitud y longitud.");
        var lat = r.Lat.Value;
        var lon = r.Lon.Value;
        var radio = Math.Clamp(r.RadioKm, 0.1, 50);
        var ahora = Ahora;

        // margen = media celda de redondeo: incluye publicaciones cuya posición aproximada cae dentro del círculo
        var (minLat, maxLat, minLon, maxLon) = Geo.Caja(lat, lon, radio, margenGrados: 0.006);
        var candidatos = await _pubs.ListarVisiblesEnAreaAsync(minLat, maxLat, minLon, maxLon, r.CategoriaId,
            r.Modo.HasValue ? (ModoTransaccion)(int)r.Modo.Value : null, MaxCandidatosCercanas, ahora);

        var cercanas = candidatos
            .Select(p =>
            {
                var esMia = actorId.HasValue && p.PropietarioId == actorId.Value;
                var pLat = esMia ? p.Latitud!.Value : Geo.Aproximar(p.Latitud!.Value);
                var pLon = esMia ? p.Longitud!.Value : Geo.Aproximar(p.Longitud!.Value);
                return (p, esMia, d: Geo.DistanciaKm(lat, lon, pLat, pLon));
            })
            .Where(x => x.d <= radio)
            .OrderBy(x => x.d)
            .Take(Math.Clamp(r.Max, 1, 50))
            .ToList();
        var favoritas = await FavoritasAsync(actorId, cercanas.Select(x => x.p));
        return cercanas.Select(x => new PublicacionCercanaDto(
                Mapeos.APublicacionDto(x.p, actorId, veExacto: x.esMia, veModeracion: false, ahora, favoritas.Contains(x.p.Id)),
                Math.Round(x.d, 1, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public async Task<IReadOnlyList<PublicacionDto>> ListarMiasAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _pubs.ListarPorPropietarioAsync(actorId);
        var favoritas = await FavoritasAsync(actorId, lista);
        return lista.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: true, veModeracion: false, ahora, favoritas.Contains(p.Id))).ToList();
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync()
        => (await _categorias.ListarAsync()).Select(Mapeos.ACategoriaDto).ToList();

    public async Task CancelarAsync(Guid actorId, Guid id, string? motivo)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");

        var ahora = Ahora;
        var enCurso = p.Estado == EstadoPublicacionEnum.EnNegociacion ? await _solicitudes.ObtenerEnCursoPorPublicacionAsync(p.Id) : null;
        const string aviso = "La publicación fue cancelada por su propietario.";
        if (enCurso?.Estado == EstadoSolicitud.Pendiente) enCurso.Rechazar(aviso);
        else if (enCurso?.Estado == EstadoSolicitud.Aceptada) enCurso.MarcarNoConcretada(aviso, ahora);
        p.Cancelar(motivo ?? "");
        await _uow.GuardarCambiosAsync();
        if (enCurso is not null)
            await _notificador.NotificarAsync(enCurso.SolicitanteId, TiposNotificacion.SolicitudRechazada,
                $"La publicación \"{p.Titulo}\" fue cancelada por su propietario.", enCurso.Id);
    }

    // ---------------- Perfil público ----------------
    private async Task<Usuario> PerfilVisibleAsync(Guid usuarioId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (u is null || SuspendidoOEliminado(u, Ahora)) throw new NoEncontradoException("Usuario no encontrado.");
        return u;
    }

    public async Task<PerfilUsuarioDto> ObtenerPerfilAsync(Guid usuarioId)
    {
        var u = await PerfilVisibleAsync(usuarioId);
        var (_, total) = await _pubs.ListarVisiblesDePropietarioAsync(usuarioId, 1, 1, Ahora);
        return Mapeos.APerfilUsuario(u, total, Ahora);
    }

    public async Task<PaginaDto<PublicacionDto>> ListarDePerfilAsync(Guid? actorId, Guid usuarioId, int pagina, int tamano)
    {
        await PerfilVisibleAsync(usuarioId);
        var ahora = Ahora;
        var (items, total) = await _pubs.ListarVisiblesDePropietarioAsync(usuarioId, pagina, tamano, ahora);
        var favoritas = await FavoritasAsync(actorId, items);
        var dtos = items.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: actorId == usuarioId, veModeracion: false, ahora,
            favoritas.Contains(p.Id))).ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }

    // ---------------- Favoritos ----------------
    public async Task MarcarFavoritaAsync(Guid actorId, Guid publicacionId)
    {
        var p = await _pubs.ObtenerPorIdAsync(publicacionId);
        var ahora = Ahora;
        if (p is null || p.EstaOculta || p.Estado != EstadoPublicacionEnum.Disponible && p.Estado != EstadoPublicacionEnum.EnNegociacion
            || SuspendidoOEliminado(p.Propietario!, ahora))
            throw new NoEncontradoException("Publicación no encontrada.");
        if (await _favoritos.ObtenerAsync(actorId, publicacionId) is not null) return; // idempotente
        if (await _favoritos.ContarAsync(actorId) >= MaxFavoritos)
            throw new ReglaDeNegocioException($"Puedes guardar como máximo {MaxFavoritos} publicaciones.");
        _favoritos.Agregar(new Favorito(actorId, publicacionId, ahora));
        try { await _uow.GuardarCambiosAsync(); }
        catch (ConflictoDeConcurrenciaException) { /* doble clic simultáneo: ya quedó guardada */ }
    }

    public async Task QuitarFavoritaAsync(Guid actorId, Guid publicacionId)
    {
        var f = await _favoritos.ObtenerAsync(actorId, publicacionId);
        if (f is null) return; // idempotente
        _favoritos.Quitar(f);
        await _uow.GuardarCambiosAsync();
    }

    /// <summary>Las que ya no están visibles (canceladas, ocultas, intercambiadas) se devuelven con su estado, para que el usuario las quite.</summary>
    public async Task<PaginaDto<PublicacionDto>> ListarFavoritasAsync(Guid actorId, int pagina, int tamano)
    {
        var (ids, total) = await _favoritos.ListarAsync(actorId, pagina, tamano);
        var pubs = (await _pubs.ListarPorIdsAsync(ids)).ToDictionary(p => p.Id);
        var ahora = Ahora;
        var dtos = ids.Where(pubs.ContainsKey).Select(id => pubs[id])
            .Where(p => !p.EstaOculta && !SuspendidoOEliminado(p.Propietario!, ahora))
            .Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: p.PropietarioId == actorId, veModeracion: false, ahora, esFavorita: true))
            .ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(pagina, 1), Math.Clamp(tamano, 1, 50));
    }
}
