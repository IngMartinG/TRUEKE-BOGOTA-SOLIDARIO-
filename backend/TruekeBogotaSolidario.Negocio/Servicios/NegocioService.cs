using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Plan Negocio (internamente <see cref="TipoCuenta.Empresa"/>, para no tocar los pagos): directorio y vitrina públicos,
/// sello "Negocio verificado" (un moderador revisa el NIT contra el RUES) y panel de estadísticas de toda la cuenta.
/// </summary>
public interface INegocioService
{
    /// <summary>Público: negocios con el plan vigente y nombre comercial (se calcula como mucho cada 2 minutos).</summary>
    Task<IReadOnlyList<NegocioResumenDto>> ListarAsync();
    /// <summary>Público: 404 si la cuenta no es un negocio vigente visible.</summary>
    Task<NegocioPublicoDto> ObtenerAsync(Guid usuarioId);
    /// <summary>Planes pagos: rendimiento de toda la cuenta en los últimos 30 días.</summary>
    Task<EstadisticasCuentaDto> EstadisticasCuentaAsync(Guid actorId);

    // Moderadores
    Task<IReadOnlyList<NegocioPorVerificarDto>> ListarPorVerificarAsync(Guid actorId);
    Task VerificarNitAsync(Guid actorId, Guid usuarioId);
    Task RechazarNitAsync(Guid actorId, Guid usuarioId, string motivo);
}

public sealed class NegocioService : INegocioService
{
    public const int MaxDirectorio = 300;
    private const int MaxPorVerificar = 100;
    private const int DiasEstadisticas = 30;
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(2);
    private const string ClaveCache = "negocios:lista";

    private readonly IUsuarioRepository _usuarios;
    private readonly IPublicacionRepository _pubs;
    private readonly IEstadisticaRepository _estadisticas;
    private readonly IFavoritoRepository _favoritos;
    private readonly ISolicitudRepository _solicitudes;
    private readonly ITransaccionRepository _transacciones;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly INotificador _notificador;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _reloj;
    private readonly ILogger<NegocioService> _log;

    public NegocioService(IUsuarioRepository usuarios, IPublicacionRepository pubs, IEstadisticaRepository estadisticas, IFavoritoRepository favoritos,
        ISolicitudRepository solicitudes, ITransaccionRepository transacciones, IAuditoriaRepository auditoria, IUnidadDeTrabajo uow,
        INotificador notificador, IMemoryCache cache, TimeProvider reloj, ILogger<NegocioService> log)
    {
        _usuarios = usuarios; _pubs = pubs; _estadisticas = estadisticas; _favoritos = favoritos; _solicitudes = solicitudes;
        _transacciones = transacciones; _auditoria = auditoria; _uow = uow; _notificador = notificador; _cache = cache; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    // ---------------- Público ----------------
    public async Task<IReadOnlyList<NegocioResumenDto>> ListarAsync()
    {
        if (_cache.TryGetValue(ClaveCache, out IReadOnlyList<NegocioResumenDto>? enCache) && enCache is not null) return enCache;
        var ahora = Ahora;
        var negocios = await _usuarios.ListarNegociosAsync(ahora, MaxDirectorio);
        var conteos = await _pubs.ContarVisiblesPorPropietarioAsync(negocios.Select(n => n.Id).ToList(), ahora);
        IReadOnlyList<NegocioResumenDto> lista = negocios.Select(u => new NegocioResumenDto(u.Id, u.NombreComercial!, Mapeos.FotoVisible(u),
            u.Localidad, Divipola.NombreCompleto(u.MunicipioCodigo), u.DescripcionNegocio, u.HorarioNegocio, u.NegocioVerificado(ahora),
            conteos.GetValueOrDefault(u.Id))).ToList();
        _cache.Set(ClaveCache, lista, DuracionCache);
        return lista;
    }

    public async Task<NegocioPublicoDto> ObtenerAsync(Guid usuarioId)
    {
        var ahora = Ahora;
        var u = await _usuarios.ObtenerPorIdAsync(usuarioId);
        if (u is null || u.EstaEliminado || u.SuspensionVigente(ahora) || !u.EmpresaVigente(ahora) || u.NombreComercial is null)
            throw new NoEncontradoException("Negocio no encontrado.");
        var publicaciones = (await _pubs.ContarVisiblesPorPropietarioAsync([u.Id], ahora)).GetValueOrDefault(u.Id);
        return new NegocioPublicoDto(u.Id, u.NombreComercial, Mapeos.FotoVisible(u), u.Localidad, Divipola.NombreCompleto(u.MunicipioCodigo),
            u.DescripcionNegocio, u.HorarioNegocio, u.NegocioVerificado(ahora), u.MostrarNit ? u.Nit : null,
            new DateTime(u.FechaRegistro.Year, u.FechaRegistro.Month, 1, 0, 0, 0, DateTimeKind.Utc), // solo mes y año
            u.CalificacionPromedio, u.CalificacionesTotal, publicaciones);
    }

    // ---------------- Panel de la cuenta ----------------
    public async Task<EstadisticasCuentaDto> EstadisticasCuentaAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        var ahora = Ahora;
        if (!u.PlanPagoVigente(ahora))
            throw new ReglaDeNegocioException("Las estadísticas de toda la cuenta son parte de los planes Premium y Negocio.");
        var desde = ahora.Date.AddDays(-(DiasEstadisticas - 1));
        var mias = await _pubs.ListarPorPropietarioAsync(actorId);
        var ids = mias.Select(p => p.Id).ToList();
        var serie = await _estadisticas.SerieDeAsync(ids, desde);
        var porDia = serie.GroupBy(e => e.Fecha.Date).ToDictionary(g => g.Key, g => g.Sum(e => e.Vistas));
        var puntos = new List<PuntoSerieDto>();
        for (var d = desde; d <= ahora.Date; d = d.AddDays(1))
            puntos.Add(new PuntoSerieDto(DateTime.SpecifyKind(d, DateTimeKind.Utc), porDia.GetValueOrDefault(d)));
        var titulos = mias.ToDictionary(p => p.Id, p => p.Titulo);
        var masVistas = serie.GroupBy(e => e.PublicacionId).Select(g => new PublicacionDestacadaDto(g.Key, titulos.GetValueOrDefault(g.Key, ""), g.Sum(e => e.Vistas)))
            .OrderByDescending(p => p.Vistas).Take(5).ToList();
        var totales = await _estadisticas.TotalesAsync(ids);
        var activas = mias.Count(p => p.Estado is EstadoPublicacionEnum.Disponible or EstadoPublicacionEnum.EnNegociacion);
        return new EstadisticasCuentaDto(puntos.Sum(p => p.Vistas), totales.Values.Sum(), await _favoritos.ContarDePublicacionesAsync(ids),
            await _solicitudes.ContarRecibidasDesdeAsync(actorId, desde), await _transacciones.ContarCompletadosDesdeAsync(actorId, desde),
            activas, puntos, masVistas);
    }

    // ---------------- Moderación del sello ----------------
    private async Task ExigirModeradorAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol < RolUsuarioEnum.Administrador) throw new AccesoDenegadoException();
    }

    public async Task<IReadOnlyList<NegocioPorVerificarDto>> ListarPorVerificarAsync(Guid actorId)
    {
        await ExigirModeradorAsync(actorId);
        return (await _usuarios.ListarNegociosPorVerificarAsync(Ahora, MaxPorVerificar))
            .Select(u => new NegocioPorVerificarDto(u.Id, u.NombreComercial!, u.Nit!, u.NombreCompleto, u.FechaVencimientoSuscripcion)).ToList();
    }

    private async Task<Usuario> NegocioParaRevisarAsync(Guid actorId, Guid usuarioId)
    {
        await ExigirModeradorAsync(actorId);
        if (actorId == usuarioId) throw new ReglaDeNegocioException("Otro moderador debe revisar tu negocio.");
        return await _usuarios.ObtenerPorIdAsync(usuarioId) ?? throw new NoEncontradoException("Usuario no encontrado.");
    }

    public async Task VerificarNitAsync(Guid actorId, Guid usuarioId)
    {
        var u = await NegocioParaRevisarAsync(actorId, usuarioId);
        var ahora = Ahora;
        u.VerificarNit(ahora);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "NEGOCIO_NIT_VERIFICADO", "Usuario", usuarioId, $"{u.NombreComercial} · {u.Nit}", ahora));
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        _log.LogInformation("NIT del negocio {UsuarioId} verificado por {ActorId}", usuarioId, actorId);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.NegocioVerificado,
            $"Revisamos el NIT de {u.NombreComercial}: tu negocio ya muestra el sello «Negocio verificado».", usuarioId);
    }

    public async Task RechazarNitAsync(Guid actorId, Guid usuarioId, string motivo)
    {
        var u = await NegocioParaRevisarAsync(actorId, usuarioId);
        var texto = (motivo ?? "").Trim();
        if (texto.Length is < 3 or > 300) throw new ReglaDeNegocioException("Escribe el motivo (entre 3 y 300 caracteres).");
        var ahora = Ahora;
        u.RechazarNit(ahora);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "NEGOCIO_NIT_NO_VERIFICADO", "Usuario", usuarioId, texto, ahora));
        await _uow.GuardarCambiosAsync();
        _cache.Remove(ClaveCache);
        await _notificador.NotificarAsync(usuarioId, TiposNotificacion.NegocioNoVerificado,
            $"No pudimos verificar el NIT de tu negocio: {texto.TrimEnd('.')}. Corrígelo en Mi cuenta → Perfil y lo revisaremos de nuevo.", usuarioId);
    }
}
