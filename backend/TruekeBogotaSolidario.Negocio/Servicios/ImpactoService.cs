using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IImpactoService
{
    /// <summary>Público: lo que la comunidad ha hecho circular. Solo totales; cada combinación de filtros se calcula como mucho cada 10 minutos.</summary>
    Task<ImpactoDto> ObtenerAsync(FiltroImpactoRequest? filtro = null);
    IReadOnlyList<ObjetoTipicoDto> ObjetosTipicos();
    /// <summary>Solo SuperUsuario: cambia un factor con su fuente y una versión nueva (lo ya registrado no cambia).</summary>
    Task<FactorImpactoDto> ActualizarFactorAsync(Guid actorId, MaterialDto material, ActualizarFactorImpactoRequest r);
    /// <summary>Solo SuperUsuario: crea la meta mensual de una localidad o cambia la que ya tiene.</summary>
    Task<MetaComunitariaDto> GuardarMetaAsync(Guid actorId, GuardarMetaComunitariaRequest r);
    Task QuitarMetaAsync(Guid actorId, Guid metaId);
}

public sealed class ImpactoService : IImpactoService
{
    /// <summary>Una localidad con menos intercambios no se muestra: con pocos casos se podría adivinar quién participó.</summary>
    public const int MinimoPorLocalidad = 3;
    private const int MaxLocalidades = 10;
    private const int MaxLocalidadesDisponibles = 50;
    private static readonly TimeSpan Duracion = TimeSpan.FromMinutes(10);
    /// <summary>Sube al cambiar un factor: las cifras en caché con la tabla vieja dejan de usarse.</summary>
    private static int _generacion;

    private readonly IImpactoRepository _impacto;
    private readonly ICategoriaRepository _categorias;
    private readonly IUsuarioRepository _usuarios;
    private readonly IAuditoriaRepository _auditoria;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ImpactoService> _log;

    public ImpactoService(IImpactoRepository impacto, ICategoriaRepository categorias, IUsuarioRepository usuarios, IAuditoriaRepository auditoria,
        IUnidadDeTrabajo uow, IMemoryCache cache, TimeProvider reloj, ILogger<ImpactoService> log)
    {
        _impacto = impacto; _categorias = categorias; _usuarios = usuarios; _auditoria = auditoria; _uow = uow; _cache = cache;
        _reloj = reloj; _log = log;
    }

    /// <summary>Inicio del periodo en hora de Colombia (UTC-5, sin horario de verano), expresado en UTC.</summary>
    public static DateTime? InicioPeriodoUtc(PeriodoImpactoDto periodo, DateTime ahoraUtc)
    {
        var local = ahoraUtc.AddHours(-5);
        DateTime? inicioLocal = periodo switch
        {
            PeriodoImpactoDto.Mes => new DateTime(local.Year, local.Month, 1),
            PeriodoImpactoDto.Trimestre => new DateTime(local.Year, (local.Month - 1) / 3 * 3 + 1, 1),
            PeriodoImpactoDto.Anio => new DateTime(local.Year, 1, 1),
            _ => null
        };
        return inicioLocal is { } i ? DateTime.SpecifyKind(i.AddHours(5), DateTimeKind.Utc) : null;
    }

    private async Task<IReadOnlyList<ImpactoLocalidad>> LocalidadesDisponiblesAsync(bool refrescar = false)
    {
        var clave = $"impacto:{_generacion}:localidades";
        if (!refrescar && _cache.TryGetValue(clave, out IReadOnlyList<ImpactoLocalidad>? lista) && lista is not null) return lista;
        lista = await _impacto.LocalidadesAsync(null, MinimoPorLocalidad, MaxLocalidadesDisponibles);
        _cache.Set(clave, lista, Duracion);
        return lista;
    }

    private static ImpactoLocalidadDto ALocalidad(ImpactoLocalidad l)
        => new(l.Localidad, Divipola.ObtenerMunicipio(l.MunicipioCodigo)?.Nombre ?? "", l.Intercambios, l.MunicipioCodigo);

    public async Task<ImpactoDto> ObtenerAsync(FiltroImpactoRequest? filtro = null)
    {
        filtro ??= new FiltroImpactoRequest();
        var disponibles = await LocalidadesDisponiblesAsync();
        // Solo localidades de la lista: así no se arma una entrada de caché por cada texto que alguien invente.
        ImpactoLocalidad? localidad = null;
        if (!string.IsNullOrWhiteSpace(filtro.Localidad))
        {
            var mpio = filtro.MunicipioCodigo ?? Divipola.CodigoBogota;
            var nombre = filtro.Localidad.Trim();
            ImpactoLocalidad? Buscar(IReadOnlyList<ImpactoLocalidad> lista) => lista.FirstOrDefault(l => l.MunicipioCodigo == mpio && l.Localidad == nombre);
            // Una localidad que acaba de llegar al mínimo no espera a que venza la caché.
            localidad = Buscar(disponibles) ?? Buscar(disponibles = await LocalidadesDisponiblesAsync(refrescar: true))
                ?? throw new ReglaDeNegocioException("Esa localidad aún no tiene suficientes intercambios para mostrarla.");
        }
        var clave = $"impacto:{_generacion}:{filtro.Periodo}:{filtro.Modo}:{localidad?.MunicipioCodigo}:{localidad?.Localidad}";
        if (_cache.TryGetValue(clave, out ImpactoDto? enCache) && enCache is not null) return enCache;

        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var modo = filtro.Modo.HasValue ? (ModoTransaccion)(int)filtro.Modo.Value : (ModoTransaccion?)null;
        var f = new FiltroImpacto(InicioPeriodoUtc(filtro.Periodo, ahora), modo, localidad?.MunicipioCodigo, localidad?.Localidad);
        var t = await _impacto.TotalesAsync(f);
        var esteMes = (await _impacto.TotalesAsync(f with { DesdeUtc = InicioPeriodoUtc(PeriodoImpactoDto.Mes, ahora) })).Intercambios;
        var reservado = localidad is not null && t.Intercambios < MinimoPorLocalidad;
        if (reservado)
        {
            t = new TotalesImpacto(0, 0, 0, 0, 0, 0, 0, 0, 0, []);
            esteMes = 0;
        }
        var masActivas = localidad is null ? await _impacto.LocalidadesAsync(f.DesdeUtc, MinimoPorLocalidad, MaxLocalidades) : [];
        var factores = await _impacto.ListarFactoresAsync();
        var categorias = await _categorias.ListarAsync();

        var dto = new ImpactoDto(t.Objetos, t.Truekes, t.Compras, t.Donaciones, t.Personas, esteMes,
            masActivas.Select(ALocalidad).ToList(), ahora,
            Math.Round(t.Kg, 1), Math.Round(t.KgCo2, 1), Math.Round(t.KgDeclarados, 1), t.Intercambios, reservado,
            VersionVigente(factores), filtro.Periodo.ToString(), filtro.Modo?.ToString(), localidad?.MunicipioCodigo, localidad?.Localidad,
            t.PorMaterial.Select(m => new ImpactoMaterialDto(m.Material.ToString(), m.Objetos, Math.Round(m.Kg, 1), Math.Round(m.KgCo2, 1))).ToList(),
            factores.Select(AFactor).ToList(),
            categorias.OrderBy(c => c.Id).Select(c =>
            {
                var tipico = ImpactoAmbiental.Tipico(c.Id);
                return new PesoCategoriaDto(c.NombreCategoria, tipico.Kg, tipico.Material.ToString());
            }).ToList(),
            disponibles.Select(ALocalidad).ToList(), await MetasAsync(ahora));
        _cache.Set(clave, dto, Duracion);
        return dto;
    }

    // ---------------- Metas comunitarias ----------------
    public const int MaxMetas = 12;

    /// <summary>Todas las metas con el avance del mes; se calculan una vez por generación de la caché (no por cada filtro).</summary>
    private async Task<IReadOnlyList<MetaComunitariaDto>> MetasAsync(DateTime ahora)
    {
        var clave = $"impacto:{_generacion}:metas";
        if (_cache.TryGetValue(clave, out IReadOnlyList<MetaComunitariaDto>? enCache) && enCache is not null) return enCache;
        var lista = new List<MetaComunitariaDto>();
        foreach (var m in await _impacto.ListarMetasAsync()) lista.Add(await AMetaAsync(m, ahora));
        _cache.Set(clave, (IReadOnlyList<MetaComunitariaDto>)lista, Duracion);
        return lista;
    }

    private async Task<MetaComunitariaDto> AMetaAsync(MetaComunitaria m, DateTime ahora)
    {
        var t = await _impacto.TotalesAsync(new FiltroImpacto(InicioPeriodoUtc(PeriodoImpactoDto.Mes, ahora), null, m.MunicipioCodigo, m.Localidad));
        var reservado = t.Intercambios < MinimoPorLocalidad;
        var kg = Math.Round(t.Kg, 1);
        return new MetaComunitariaDto(m.Id, m.Localidad, Divipola.ObtenerMunicipio(m.MunicipioCodigo)?.Nombre ?? "", m.MunicipioCodigo, m.MetaKg,
            reservado ? null : kg, reservado ? null : (int)Math.Min(100, Math.Floor(kg * 100 / m.MetaKg)), reservado);
    }

    private async Task ExigirSuperUsuarioAsync(Guid actorId)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol != RolUsuarioEnum.SuperUsuario) throw new AccesoDenegadoException();
    }

    public async Task<MetaComunitariaDto> GuardarMetaAsync(Guid actorId, GuardarMetaComunitariaRequest r)
    {
        await ExigirSuperUsuarioAsync(actorId);
        var mpio = r.MunicipioCodigo ?? Divipola.CodigoBogota;
        var nombre = r.Localidad.Trim();
        // Solo localidades con datos suficientes: así la meta coincide con el texto exacto con que se registra el impacto
        ImpactoLocalidad? Buscar(IReadOnlyList<ImpactoLocalidad> l) => l.FirstOrDefault(x => x.MunicipioCodigo == mpio && x.Localidad == nombre);
        var localidad = Buscar(await LocalidadesDisponiblesAsync()) ?? Buscar(await LocalidadesDisponiblesAsync(refrescar: true))
            ?? throw new ReglaDeNegocioException($"Esa localidad aún no tiene {MinimoPorLocalidad} intercambios: elige una de la lista.");
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var meta = await _impacto.MetaDeLocalidadAsync(localidad.MunicipioCodigo, localidad.Localidad);
        var antes = meta?.MetaKg;
        if (meta is null)
        {
            if ((await _impacto.ListarMetasAsync()).Count >= MaxMetas) throw new ReglaDeNegocioException($"Puedes tener como máximo {MaxMetas} metas.");
            meta = new MetaComunitaria(localidad.MunicipioCodigo, localidad.Localidad, r.MetaKg, actorId, ahora);
            _impacto.AgregarMeta(meta);
        }
        else meta.CambiarMeta(r.MetaKg);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "META_COMUNITARIA_GUARDADA", "MetaComunitaria", meta.Id,
            $"{meta.Localidad}: {(antes is null ? "nueva" : $"{antes} kg")} → {meta.MetaKg} kg", ahora));
        await _uow.GuardarCambiosAsync();
        Interlocked.Increment(ref _generacion);
        return await AMetaAsync(meta, ahora);
    }

    public async Task QuitarMetaAsync(Guid actorId, Guid metaId)
    {
        await ExigirSuperUsuarioAsync(actorId);
        var meta = await _impacto.ObtenerMetaAsync(metaId) ?? throw new NoEncontradoException("Meta no encontrada.");
        _impacto.QuitarMeta(meta);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "META_COMUNITARIA_QUITADA", "MetaComunitaria", meta.Id,
            $"{meta.Localidad}: {meta.MetaKg} kg", _reloj.GetUtcNow().UtcDateTime));
        await _uow.GuardarCambiosAsync();
        Interlocked.Increment(ref _generacion);
    }

    private static FactorImpactoDto AFactor(FactorImpacto f) => new(f.Material.ToString(), f.KgCo2PorKg, f.Fuente, f.Version);

    /// <summary>La versión más alta entre los factores vigentes (comparando cada número, no el texto).</summary>
    public static string VersionVigente(IEnumerable<FactorImpacto> factores)
        => factores.Select(f => f.Version).MaxBy(v => Version.TryParse(v, out var x) ? x : new Version(0, 0)) ?? "2.0.0";

    public IReadOnlyList<ObjetoTipicoDto> ObjetosTipicos()
        => ImpactoAmbiental.ObjetosTipicos.Select(o => new ObjetoTipicoDto(o.Nombre, o.Kg, o.Material.ToString(), o.CategoriaId)).ToList();

    public async Task<FactorImpactoDto> ActualizarFactorAsync(Guid actorId, MaterialDto material, ActualizarFactorImpactoRequest r)
    {
        var actor = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (actor.Rol != RolUsuarioEnum.SuperUsuario) throw new AccesoDenegadoException();
        var factor = await _impacto.ObtenerFactorAsync((MaterialObjeto)(int)material) ?? throw new NoEncontradoException("Material no encontrado.");
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var antes = $"{factor.KgCo2PorKg} ({factor.Version})";
        factor.Actualizar(r.KgCo2PorKg, r.Fuente, r.Version, actorId, ahora);
        _auditoria.Agregar(new AuditoriaEvento(actorId, "FACTOR_IMPACTO_ACTUALIZADO", "FactorImpacto", Guid.Empty,
            $"{factor.Material}: {antes} → {factor.KgCo2PorKg} ({factor.Version})", ahora));
        await _uow.GuardarCambiosAsync();
        Interlocked.Increment(ref _generacion);
        _log.LogInformation("Factor de impacto {Material} actualizado a la versión {Version} por {ActorId}", factor.Material, factor.Version, actorId);
        return AFactor(factor);
    }
}
