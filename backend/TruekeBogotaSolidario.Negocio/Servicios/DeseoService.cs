using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IDeseoService
{
    Task<IReadOnlyList<DeseoDto>> ListarAsync(Guid actorId);
    Task<DeseoDto> CrearAsync(Guid actorId, GuardarDeseoRequest r);
    Task<DeseoDto> ActualizarAsync(Guid actorId, Guid id, GuardarDeseoRequest r);
    Task EliminarAsync(Guid actorId, Guid id);
    /// <summary>Publicaciones de otras personas que coinciden con mis deseos; primero los truekes mutuos.</summary>
    Task<IReadOnlyList<CoincidenciaDeseoDto>> CoincidenciasAsync(Guid actorId);
    /// <summary>Lo que se muestra en el perfil (públicos y no ocultos). <c>LoTienes</c> solo se calcula con sesión.</summary>
    Task<IReadOnlyList<DeseoPublicoDto>> VisiblesDePerfilAsync(Guid? actorId, Guid usuarioId);
    /// <summary>Tarea periódica: avisa cuando alguien publica algo que coincide con un deseo. Devuelve cuántos avisos envió.</summary>
    Task<int> RevisarAvisosAsync(int maximo);
}

public sealed class DeseoService : IDeseoService
{
    /// <summary>Como mucho un aviso por deseo en este intervalo (la tarea de mantenimiento corre cada hora).</summary>
    public static readonly TimeSpan IntervaloAvisos = TimeSpan.FromMinutes(55);
    /// <summary>Publicaciones que se traen por cada deseo al calcular las coincidencias.</summary>
    public const int CandidatasPorDeseo = 12;
    public const int MaxCoincidencias = 40;
    /// <summary>Mis publicaciones disponibles que se comparan con los deseos de otras personas.</summary>
    private const int MaxPropiasComparadas = 50;

    private readonly IDeseoRepository _deseos;
    private readonly IPublicacionRepository _pubs;
    private readonly ICategoriaRepository _categorias;
    private readonly IUsuarioRepository _usuarios;
    private readonly IBloqueoRepository _bloqueos;
    private readonly IFavoritoRepository _favoritos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DeseoService> _log;

    public DeseoService(IDeseoRepository deseos, IPublicacionRepository pubs, ICategoriaRepository categorias, IUsuarioRepository usuarios,
        IBloqueoRepository bloqueos, IFavoritoRepository favoritos, IUnidadDeTrabajo uow, INotificador notificador, TimeProvider reloj,
        ILogger<DeseoService> log)
    {
        _deseos = deseos; _pubs = pubs; _categorias = categorias; _usuarios = usuarios; _bloqueos = bloqueos; _favoritos = favoritos;
        _uow = uow; _notificador = notificador; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<DeseoDto>> ListarAsync(Guid actorId)
    {
        var categorias = await NombresCategoriasAsync();
        return (await _deseos.DeUsuarioAsync(actorId)).Select(d => ADto(d, categorias)).ToList();
    }

    public async Task<DeseoDto> CrearAsync(Guid actorId, GuardarDeseoRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor); // es contenido público, como un comentario
        await ValidarCategoriaAsync(r.CategoriaId);
        var nuevo = new Deseo(actorId, r.Texto, r.CategoriaId, r.Publico, r.Avisos, Ahora);
        var actuales = await _deseos.DeUsuarioAsync(actorId);
        if (actuales.Any(d => MismaBusqueda(d, nuevo)))
            throw new ReglaDeNegocioException("Ya tienes ese deseo en tu lista.");
        if (actuales.Count >= Deseo.MaxPorUsuario)
            throw new ReglaDeNegocioException($"Tu lista admite hasta {Deseo.MaxPorUsuario} deseos. Borra alguno para agregar este.");
        _deseos.Agregar(nuevo);
        await _uow.GuardarCambiosAsync();
        return ADto(nuevo, await NombresCategoriasAsync());
    }

    public async Task<DeseoDto> ActualizarAsync(Guid actorId, Guid id, GuardarDeseoRequest r)
    {
        var d = await PropioAsync(actorId, id);
        await ValidarCategoriaAsync(r.CategoriaId);
        d.Editar(r.Texto, r.CategoriaId, r.Publico, r.Avisos, Ahora);
        if ((await _deseos.DeUsuarioAsync(actorId)).Any(o => o.Id != d.Id && MismaBusqueda(o, d)))
            throw new ReglaDeNegocioException("Ya tienes ese deseo en tu lista.");
        await _uow.GuardarCambiosAsync();
        return ADto(d, await NombresCategoriasAsync());
    }

    public async Task EliminarAsync(Guid actorId, Guid id)
    {
        _deseos.Quitar(await PropioAsync(actorId, id));
        await _uow.GuardarCambiosAsync();
    }

    public async Task<IReadOnlyList<CoincidenciaDeseoDto>> CoincidenciasAsync(Guid actorId)
    {
        var mios = await _deseos.DeUsuarioAsync(actorId);
        if (mios.Count == 0) return Array.Empty<CoincidenciaDeseoDto>();
        var ahora = Ahora;
        var bloqueados = await _bloqueos.RelacionadosAsync(actorId);

        // 1. Lo que otras personas tienen y yo busco (misma búsqueda que el catálogo).
        var candidatas = new Dictionary<Guid, (Publicacion Pub, Deseo Deseo)>();
        foreach (var d in mios)
        {
            var filtro = new FiltroPublicaciones
            {
                Texto = d.Texto, CategoriaId = d.CategoriaId, ExcluirPropietarioId = actorId, Tamano = CandidatasPorDeseo
            };
            var (items, _) = await _pubs.ListarVisiblesAsync(filtro, ahora);
            foreach (var p in items.Where(p => !bloqueados.Contains(p.PropietarioId)))
                candidatas.TryAdd(p.Id, (p, d));
        }
        if (candidatas.Count == 0) return Array.Empty<CoincidenciaDeseoDto>();

        // 2. Si es un trueke, ¿esa persona busca algo que yo tengo disponible? Solo cuentan sus deseos públicos.
        var (propias, _) = await _pubs.ListarVisiblesDePropietarioAsync(actorId, 1, MaxPropiasComparadas, ahora);
        var duenos = candidatas.Values.Where(c => c.Pub.Modo == ModoTransaccion.Trueke).Select(c => c.Pub.PropietarioId).Distinct().ToList();
        var deseosAjenos = propias.Count == 0 || duenos.Count == 0
            ? new Dictionary<Guid, List<Deseo>>()
            : (await _deseos.VisiblesDeUsuariosAsync(duenos)).GroupBy(d => d.UsuarioId).ToDictionary(g => g.Key, g => g.ToList());

        var favoritas = await _favoritos.FiltrarFavoritasAsync(actorId, candidatas.Keys.ToList());
        var resultado = new List<(CoincidenciaDeseoDto Dto, DateTime Relevancia)>();
        foreach (var (pub, deseo) in candidatas.Values)
        {
            (Deseo Suyo, Publicacion Mia)? mutua = null;
            if (pub.Modo == ModoTransaccion.Trueke && deseosAjenos.TryGetValue(pub.PropietarioId, out var suyos))
                mutua = suyos.SelectMany(s => propias.Where(s.CoincideCon).Select(m => ((Deseo, Publicacion)?)(s, m))).FirstOrDefault();
            var dto = new CoincidenciaDeseoDto(
                Mapeos.APublicacionDto(pub, actorId, veExacto: false, veModeracion: false, ahora, favoritas.Contains(pub.Id)),
                deseo.Texto, mutua is not null, mutua?.Suyo.Texto, mutua?.Mia.Id, mutua?.Mia.Titulo);
            resultado.Add((dto, pub.FechaRelevancia));
        }
        return resultado.OrderByDescending(x => x.Dto.Mutua).ThenByDescending(x => x.Relevancia).ThenBy(x => x.Dto.Publicacion.Id)
            .Take(MaxCoincidencias).Select(x => x.Dto).ToList();
    }

    public async Task<IReadOnlyList<DeseoPublicoDto>> VisiblesDePerfilAsync(Guid? actorId, Guid usuarioId)
    {
        var visibles = await _deseos.VisiblesDeUsuariosAsync(new[] { usuarioId });
        if (visibles.Count == 0) return Array.Empty<DeseoPublicoDto>();
        IReadOnlyList<Publicacion> propias = Array.Empty<Publicacion>();
        if (actorId is { } actor && actor != usuarioId && !await _bloqueos.ExisteEntreAsync(actor, usuarioId))
            (propias, _) = await _pubs.ListarVisiblesDePropietarioAsync(actor, 1, MaxPropiasComparadas, Ahora);
        var categorias = await NombresCategoriasAsync();
        return visibles.Select(d => new DeseoPublicoDto(d.Id, d.Texto, NombreCategoria(d.CategoriaId, categorias), propias.Any(d.CoincideCon)))
            .ToList();
    }

    public async Task<int> RevisarAvisosAsync(int maximo)
    {
        var ahora = Ahora;
        var pendientes = await _deseos.ListarParaAvisosAsync(ahora - IntervaloAvisos, maximo);
        if (pendientes.Count == 0) return 0;
        // Con una sola publicación, el aviso lleva a ella; con varias, a la lista de coincidencias (sin recurso).
        var avisos = new List<(Guid Usuario, string Mensaje, Guid? Recurso)>();
        var bloqueosPorUsuario = new Dictionary<Guid, IReadOnlySet<Guid>>();
        foreach (var d in pendientes)
        {
            var filtro = new FiltroPublicaciones
            {
                Texto = d.Texto, CategoriaId = d.CategoriaId, ExcluirPropietarioId = d.UsuarioId,
                PublicadasDesde = d.RevisadoHastaUtc,
                PublicadasHasta = ahora, // lo publicado justo después se revisa la próxima vez: nada se pierde ni se repite
                Tamano = 10
            };
            var (items, _) = await _pubs.ListarVisiblesAsync(filtro, ahora);
            d.MarcarRevisado(ahora);
            if (items.Count == 0) continue;
            if (!bloqueosPorUsuario.TryGetValue(d.UsuarioId, out var bloqueados))
                bloqueosPorUsuario[d.UsuarioId] = bloqueados = await _bloqueos.RelacionadosAsync(d.UsuarioId);
            var nuevas = items.Where(p => !bloqueados.Contains(p.PropietarioId)).ToList();
            if (nuevas.Count == 0) continue;
            avisos.Add(nuevas.Count == 1
                ? (d.UsuarioId, $"Alguien publicó «{nuevas[0].Titulo}», que coincide con lo que buscas: «{d.Texto}».", (Guid?)nuevas[0].Id)
                : (d.UsuarioId, $"Hay {nuevas.Count} publicaciones nuevas que coinciden con lo que buscas: «{d.Texto}».", null));
        }
        await _uow.GuardarCambiosAsync(); // primero se marca: si algo falla después, nunca se avisa dos veces
        foreach (var a in avisos) await _notificador.NotificarAsync(a.Usuario, TiposNotificacion.DeseoCoincidencia, a.Mensaje, a.Recurso);
        if (avisos.Count > 0) _log.LogInformation("Avisos de la lista de deseos enviados: {Total}", avisos.Count);
        return avisos.Count;
    }

    private async Task<Deseo> PropioAsync(Guid actorId, Guid id)
    {
        var d = await _deseos.ObtenerAsync(id);
        if (d is null || d.UsuarioId != actorId) throw new NoEncontradoException("Deseo no encontrado.");
        return d;
    }

    private async Task ValidarCategoriaAsync(int? categoriaId)
    {
        if (categoriaId is { } c && await _categorias.ObtenerPorIdAsync(c) is null) throw new ReglaDeNegocioException("La categoría no existe.");
    }

    private static bool MismaBusqueda(Deseo a, Deseo b)
        => a.CategoriaId == b.CategoriaId
           && TextoBusqueda.Terminos(a.Texto).Order(StringComparer.Ordinal).SequenceEqual(TextoBusqueda.Terminos(b.Texto).Order(StringComparer.Ordinal));

    private async Task<IReadOnlyDictionary<int, string>> NombresCategoriasAsync()
        => (await _categorias.ListarAsync()).ToDictionary(c => c.Id, c => c.NombreCategoria);

    private static string? NombreCategoria(int? id, IReadOnlyDictionary<int, string> categorias)
        => id is { } c && categorias.TryGetValue(c, out var nombre) ? nombre : null;

    private static DeseoDto ADto(Deseo d, IReadOnlyDictionary<int, string> categorias)
        => new(d.Id, d.Texto, d.CategoriaId, NombreCategoria(d.CategoriaId, categorias), d.Publico, d.AvisosActivos, d.EstaOculto,
            d.MotivoOcultamiento, d.FechaUtc);
}
