using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IBusquedaGuardadaService
{
    Task<IReadOnlyList<BusquedaGuardadaDto>> ListarAsync(Guid actorId);
    /// <summary>Si ya tenía la misma búsqueda, devuelve esa (sin duplicar).</summary>
    Task<BusquedaGuardadaDto> GuardarAsync(Guid actorId, GuardarBusquedaRequest r);
    Task<BusquedaGuardadaDto> CambiarAlertasAsync(Guid actorId, Guid id, bool activas);
    Task EliminarAsync(Guid actorId, Guid id);
    /// <summary>Tarea periódica: avisa de publicaciones nuevas en las búsquedas con alertas. Devuelve cuántos avisos envió.</summary>
    Task<int> RevisarAlertasAsync(int maximo);
}

public sealed class BusquedaGuardadaService : IBusquedaGuardadaService
{
    /// <summary>Como mucho un aviso por búsqueda en este intervalo (la tarea de mantenimiento corre cada hora).</summary>
    public static readonly TimeSpan IntervaloAlertas = TimeSpan.FromMinutes(55);

    private readonly IBusquedaGuardadaRepository _busquedas;
    private readonly IPublicacionRepository _pubs;
    private readonly ICategoriaRepository _categorias;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly TimeProvider _reloj;
    private readonly ILogger<BusquedaGuardadaService> _log;

    public BusquedaGuardadaService(IBusquedaGuardadaRepository busquedas, IPublicacionRepository pubs, ICategoriaRepository categorias,
        IUsuarioRepository usuarios, IUnidadDeTrabajo uow, INotificador notificador, TimeProvider reloj, ILogger<BusquedaGuardadaService> log)
    {
        _busquedas = busquedas; _pubs = pubs; _categorias = categorias; _usuarios = usuarios; _uow = uow; _notificador = notificador;
        _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<BusquedaGuardadaDto>> ListarAsync(Guid actorId)
    {
        var categorias = await NombresCategoriasAsync();
        return (await _busquedas.DeUsuarioAsync(actorId)).Select(b => ADto(b, categorias)).ToList();
    }

    public async Task<BusquedaGuardadaDto> GuardarAsync(Guid actorId, GuardarBusquedaRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(actor); // las alertas llegan a una cuenta real
        if (r.CategoriaId is { } c && await _categorias.ObtenerPorIdAsync(c) is null) throw new ReglaDeNegocioException("La categoría no existe.");
        if (r.MunicipioCodigo is not null) Divipola.Exigir(r.MunicipioCodigo);

        var nueva = new BusquedaGuardada(actorId, r.Texto, r.CategoriaId, r.Modo.HasValue ? (ModoTransaccion)(int)r.Modo.Value : null,
            r.Condicion.HasValue ? (CondicionProducto)(int)r.Condicion.Value : null, r.DepartamentoCodigo, r.MunicipioCodigo, r.Localidad,
            r.PrecioMin, r.PrecioMax, r.SoloVerificados, r.Alertas, Ahora);
        var actuales = await _busquedas.DeUsuarioAsync(actorId);
        var categorias = await NombresCategoriasAsync();
        if (actuales.FirstOrDefault(b => b.EquivaleA(nueva)) is { } existente) return ADto(existente, categorias);
        if (actuales.Count >= BusquedaGuardada.MaxPorUsuario)
            throw new ReglaDeNegocioException($"Puedes guardar hasta {BusquedaGuardada.MaxPorUsuario} búsquedas. Borra alguna para guardar esta.");

        _busquedas.Agregar(nueva);
        await _uow.GuardarCambiosAsync();
        return ADto(nueva, categorias);
    }

    private async Task<BusquedaGuardada> PropiaAsync(Guid actorId, Guid id)
    {
        var b = await _busquedas.ObtenerAsync(id);
        if (b is null || b.UsuarioId != actorId) throw new NoEncontradoException("Búsqueda no encontrada.");
        return b;
    }

    public async Task<BusquedaGuardadaDto> CambiarAlertasAsync(Guid actorId, Guid id, bool activas)
    {
        var b = await PropiaAsync(actorId, id);
        b.CambiarAlertas(activas, Ahora);
        await _uow.GuardarCambiosAsync();
        return ADto(b, await NombresCategoriasAsync());
    }

    public async Task EliminarAsync(Guid actorId, Guid id)
    {
        _busquedas.Quitar(await PropiaAsync(actorId, id));
        await _uow.GuardarCambiosAsync();
    }

    public async Task<int> RevisarAlertasAsync(int maximo)
    {
        var ahora = Ahora;
        var pendientes = await _busquedas.ListarParaAlertasAsync(ahora - IntervaloAlertas, maximo);
        if (pendientes.Count == 0) return 0;
        var categorias = await NombresCategoriasAsync();
        var avisos = new List<(Guid Usuario, string Mensaje, Guid Busqueda)>();
        foreach (var b in pendientes)
        {
            var filtro = b.AFiltro();
            filtro.PublicadasDesde = b.RevisadaHastaUtc;
            filtro.PublicadasHasta = ahora; // lo publicado justo después se revisa la próxima vez: nada se pierde ni se repite
            filtro.ExcluirPropietarioId = b.UsuarioId;
            filtro.Tamano = 1;
            var (_, nuevas) = await _pubs.ListarVisiblesAsync(filtro, ahora);
            b.MarcarRevisada(ahora);
            if (nuevas > 0)
                avisos.Add((b.UsuarioId, nuevas == 1
                    ? $"Hay 1 publicación nueva para tu búsqueda «{Nombre(b, categorias)}»."
                    : $"Hay {nuevas} publicaciones nuevas para tu búsqueda «{Nombre(b, categorias)}».", b.Id));
        }
        await _uow.GuardarCambiosAsync(); // primero se marca: si algo falla después, nunca se avisa dos veces
        foreach (var a in avisos) await _notificador.NotificarAsync(a.Usuario, TiposNotificacion.BusquedaGuardada, a.Mensaje, a.Busqueda);
        if (avisos.Count > 0) _log.LogInformation("Alertas de búsquedas guardadas enviadas: {Total}", avisos.Count);
        return avisos.Count;
    }

    private async Task<IReadOnlyDictionary<int, string>> NombresCategoriasAsync()
        => (await _categorias.ListarAsync()).ToDictionary(c => c.Id, c => c.NombreCategoria);

    /// <summary>"bicicleta · Trueke · Tecnología · Medellín, Antioquia · hasta $200.000"</summary>
    public static string Nombre(BusquedaGuardada b, IReadOnlyDictionary<int, string> categorias)
    {
        var partes = new List<string>();
        if (b.Texto is not null) partes.Add(b.Texto);
        if (b.Modo is { } m) partes.Add(m switch { ModoTransaccion.Donacion => "Donación", ModoTransaccion.Compra => "Compra y venta", _ => "Trueke" });
        if (b.CategoriaId is { } c && categorias.TryGetValue(c, out var cat)) partes.Add(cat);
        if (b.MunicipioCodigo is not null) partes.Add(Divipola.NombreCompleto(b.MunicipioCodigo));
        else if (Divipola.ObtenerDepartamento(b.DepartamentoCodigo) is { } d) partes.Add(d.Nombre);
        if (b.Localidad is not null) partes.Add(b.Localidad);
        var colombia = System.Globalization.CultureInfo.GetCultureInfo("es-CO");
        if (b.PrecioMax is { } max) partes.Add($"hasta ${max.ToString("N0", colombia)}");
        else if (b.PrecioMin is { } min) partes.Add($"desde ${min.ToString("N0", colombia)}");
        if (b.SoloVerificados) partes.Add("verificados");
        return partes.Count > 0 ? string.Join(" · ", partes) : "Todo el catálogo";
    }

    private static BusquedaGuardadaDto ADto(BusquedaGuardada b, IReadOnlyDictionary<int, string> categorias)
        => new(b.Id, Nombre(b, categorias), b.Texto, b.CategoriaId, b.Modo?.ToString(), b.Condicion?.ToString(), b.DepartamentoCodigo,
            b.MunicipioCodigo, b.Localidad, b.PrecioMin, b.PrecioMax, b.SoloVerificados, b.AlertasActivas, b.FechaUtc);
}
