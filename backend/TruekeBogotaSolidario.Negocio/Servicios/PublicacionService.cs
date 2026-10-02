using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IPublicacionService
{
    Task<PublicacionDto> CrearAsync(Guid actorId, CrearPublicacionRequest r);
    Task<PaginaDto<PublicacionDto>> ListarAsync(Guid? actorId, FiltroPublicacionesRequest f);
    Task<PublicacionDto> ObtenerAsync(Guid? actorId, Guid id);
    Task<IReadOnlyList<PublicacionCercanaDto>> ListarCercanasAsync(Guid? actorId, CercanasRequest r);
    Task<IReadOnlyList<PublicacionDto>> ListarMiasAsync(Guid actorId);
    Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync();
    Task CancelarAsync(Guid actorId, Guid id, string? motivo);
}

public sealed class PublicacionService : IPublicacionService
{
    private readonly IPublicacionRepository _pubs;
    private readonly IUsuarioRepository _usuarios;
    private readonly ICategoriaRepository _categorias;
    private readonly ISolicitudRepository _solicitudes;
    private readonly IUnidadDeTrabajo _uow;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;
    private readonly INotificador _notificador;

    public PublicacionService(IPublicacionRepository pubs, IUsuarioRepository usuarios, ICategoriaRepository categorias,
        ISolicitudRepository solicitudes, IUnidadDeTrabajo uow, IOptions<UrlsOpciones> urls, TimeProvider reloj, INotificador notificador)
    {
        _pubs = pubs; _usuarios = usuarios; _categorias = categorias; _solicitudes = solicitudes; _uow = uow; _urls = urls.Value; _reloj = reloj;
        _notificador = notificador;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<PublicacionDto> CrearAsync(Guid actorId, CrearPublicacionRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor);
        if (await _pubs.ContarActivasPorUsuarioAsync(actorId) >= Limites.MaxPublicacionesActivasPorUsuario)
            throw new ReglaDeNegocioException($"Alcanzaste el máximo de {Limites.MaxPublicacionesActivasPorUsuario} publicaciones activas.");

        var categoria = await _categorias.ObtenerPorIdAsync(r.CategoriaId) ?? throw new ReglaDeNegocioException("La categoría no existe.");
        ValidadorUrls.ExigirHostPermitido(r.ImagenUrl, _urls.HostsPermitidosImagenes, "La imagen");

        var pub = new Publicacion(actorId, r.Titulo, r.Descripcion, categoria, (ModoTransaccion)(int)r.Modo, r.Localidad,
            r.PrecioReferenciaCop, r.Latitud, r.Longitud, r.ImagenUrl);
        _pubs.Agregar(pub);
        await _uow.GuardarCambiosAsync();

        var creada = await _pubs.ObtenerPorIdAsync(pub.Id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        return Mapeos.APublicacionDto(creada, actorId, veExacto: true, veModeracion: false, Ahora);
    }

    public async Task<PaginaDto<PublicacionDto>> ListarAsync(Guid? actorId, FiltroPublicacionesRequest f)
    {
        var ahora = Ahora;
        var filtro = new FiltroPublicaciones
        {
            CategoriaId = f.CategoriaId,
            Modo = f.Modo.HasValue ? (ModoTransaccion)(int)f.Modo.Value : null,
            Localidad = f.Localidad,
            Texto = f.Texto,
            Pagina = f.Pagina,
            Tamano = f.Tamano
        };
        var (items, total) = await _pubs.ListarVisiblesAsync(filtro, ahora);
        var dtos = items.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: actorId.HasValue && p.PropietarioId == actorId, veModeracion: false, ahora)).ToList();
        return new PaginaDto<PublicacionDto>(dtos, total, Math.Max(f.Pagina, 1), Math.Clamp(f.Tamano, 1, 50));
    }

    public async Task<PublicacionDto> ObtenerAsync(Guid? actorId, Guid id)
    {
        var p = await _pubs.ObtenerPorIdAsync(id) ?? throw new NoEncontradoException("Publicación no encontrada.");
        var actor = actorId.HasValue ? await _usuarios.ObtenerPorIdAsync(actorId.Value) : null;

        var esDueno = actor is not null && p.PropietarioId == actor.Id;
        var esModerador = actor is not null && actor.Rol >= RolUsuarioEnum.Administrador;
        var aceptada = actor is not null && !esDueno && await _solicitudes.ExisteAceptadaAsync(p.Id, actor.Id);
        var visiblePublicamente = p.Estado == EstadoPublicacionEnum.Disponible && !p.EstaOculta;

        // Lo que no puede ver, "no existe" para el usuario (no se filtra información por códigos distintos)
        if (!visiblePublicamente && !esDueno && !esModerador && !aceptada)
            throw new NoEncontradoException("Publicación no encontrada.");

        return Mapeos.APublicacionDto(p, actor?.Id, veExacto: esDueno || esModerador || aceptada, veModeracion: esModerador, Ahora);
    }

    private const int MaxCandidatosCercanas = 500;

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
            r.Modo.HasValue ? (ModoTransaccion)(int)r.Modo.Value : null, MaxCandidatosCercanas);

        return candidatos
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
            .Select(x => new PublicacionCercanaDto(
                Mapeos.APublicacionDto(x.p, actorId, veExacto: x.esMia, veModeracion: false, ahora),
                Math.Round(x.d, 1, MidpointRounding.AwayFromZero)))
            .ToList();
    }

    public async Task<IReadOnlyList<PublicacionDto>> ListarMiasAsync(Guid actorId)
    {
        var ahora = Ahora;
        var lista = await _pubs.ListarPorPropietarioAsync(actorId);
        return lista.Select(p => Mapeos.APublicacionDto(p, actorId, veExacto: true, veModeracion: false, ahora)).ToList();
    }

    public async Task<IReadOnlyList<CategoriaDto>> ListarCategoriasAsync()
        => (await _categorias.ListarAsync()).Select(Mapeos.ACategoriaDto).ToList();

    public async Task CancelarAsync(Guid actorId, Guid id, string? motivo)
    {
        var p = await _pubs.ObtenerPorIdAsync(id);
        if (p is null || p.PropietarioId != actorId) throw new NoEncontradoException("Publicación no encontrada.");

        var estabaEnNegociacion = p.Estado == EstadoPublicacionEnum.EnNegociacion;
        p.Cancelar(motivo ?? "");
        Solicitud? pendiente = null;
        if (estabaEnNegociacion)
        {
            pendiente = await _solicitudes.ObtenerPendientePorPublicacionAsync(p.Id);
            pendiente?.Rechazar("La publicación fue cancelada por su propietario.");
        }
        await _uow.GuardarCambiosAsync();
        if (pendiente is not null)
            await _notificador.NotificarAsync(pendiente.SolicitanteId, new NotificacionDto(TiposNotificacion.SolicitudRechazada,
                $"La publicación \"{p.Titulo}\" fue cancelada por su propietario.", pendiente.Id, Ahora));
    }
}
