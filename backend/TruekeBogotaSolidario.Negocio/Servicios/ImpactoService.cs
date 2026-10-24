using Microsoft.Extensions.Caching.Memory;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IImpactoService
{
    /// <summary>Público: lo que la comunidad ha hecho circular. Solo totales; se calcula como mucho cada 10 minutos.</summary>
    Task<ImpactoDto> ObtenerAsync();
}

public sealed class ImpactoService : IImpactoService
{
    /// <summary>Una localidad con menos intercambios no se muestra: con pocos casos se podría adivinar quién participó.</summary>
    public const int MinimoPorLocalidad = 3;
    private const int MaxLocalidades = 10;
    private static readonly TimeSpan Duracion = TimeSpan.FromMinutes(10);
    private const string Clave = "impacto:resumen";

    private readonly ITransaccionRepository _transacciones;
    private readonly ICategoriaRepository _categorias;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _reloj;

    public ImpactoService(ITransaccionRepository transacciones, ICategoriaRepository categorias, IMemoryCache cache, TimeProvider reloj)
    {
        _transacciones = transacciones; _categorias = categorias; _cache = cache; _reloj = reloj;
    }

    public async Task<ImpactoDto> ObtenerAsync()
    {
        if (_cache.TryGetValue(Clave, out ImpactoDto? enCache) && enCache is not null) return enCache;
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        // "Este mes" en hora de Colombia (UTC-5, sin horario de verano)
        var local = ahora.AddHours(-5);
        var inicioMesUtc = new DateTime(local.Year, local.Month, 1, 5, 0, 0, DateTimeKind.Utc);
        var r = await _transacciones.ResumenImpactoAsync(inicioMesUtc, MinimoPorLocalidad, MaxLocalidades);
        var (_, kg, co2) = ImpactoAmbiental.Estimar(r.PorCategoria);
        var factores = (await _categorias.ListarAsync()).OrderBy(c => c.Id)
            .Select(c => { var f = ImpactoAmbiental.Factor(c.Id); return new FactorImpactoDto(c.NombreCategoria, f.KgPorObjeto, f.KgCo2PorObjeto); })
            .ToList();
        var dto = new ImpactoDto(r.Truekes + r.Compras + r.Donaciones, r.Truekes, r.Compras, r.Donaciones, r.Personas, r.EsteMes,
            r.Localidades.Select(l => new ImpactoLocalidadDto(l.Localidad, Divipola.ObtenerMunicipio(l.MunicipioCodigo)?.Nombre ?? "", l.Intercambios)).ToList(),
            ahora, kg, co2, factores);
        _cache.Set(Clave, dto, Duracion);
        return dto;
    }
}
