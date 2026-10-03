using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Catálogo de departamentos y municipios de Colombia (DANE - DIVIPOLA) para los selectores del front.</summary>
public interface IUbicacionService
{
    IReadOnlyList<DepartamentoDto> Departamentos();
    /// <summary>404 si el departamento no existe.</summary>
    IReadOnlyList<MunicipioDto> MunicipiosDe(string departamentoCodigo);
    /// <summary>Búsqueda por nombre sin tildes ni mayúsculas ("cucuta" → San José de Cúcuta). Capitales y Bogotá primero.</summary>
    IReadOnlyList<MunicipioDto> Buscar(string texto, int maximo);
    MunicipioDto Obtener(string codigo);
}

public sealed class UbicacionService : IUbicacionService
{
    private static MunicipioDto ADto(Municipio m)
        => new(m.Codigo, m.Nombre, m.DepartamentoCodigo, Divipola.ObtenerDepartamento(m.DepartamentoCodigo)?.Nombre ?? "", m.Latitud, m.Longitud);

    public IReadOnlyList<DepartamentoDto> Departamentos() => Divipola.Departamentos.Select(d => new DepartamentoDto(d.Codigo, d.Nombre)).ToList();

    public IReadOnlyList<MunicipioDto> MunicipiosDe(string departamentoCodigo)
    {
        if (Divipola.ObtenerDepartamento(departamentoCodigo) is null) throw new NoEncontradoException("Departamento no encontrado.");
        return Divipola.MunicipiosDe(departamentoCodigo).Select(ADto).ToList();
    }

    public IReadOnlyList<MunicipioDto> Buscar(string texto, int maximo)
    {
        var t = Divipola.Normalizar(texto ?? "");
        if (t.Length < 2) return Array.Empty<MunicipioDto>();
        return Divipola.Municipios
            .Select(m => (m, nombre: Divipola.Normalizar(m.Nombre)))
            .Where(x => x.nombre.Contains(t, StringComparison.Ordinal))
            .OrderBy(x => x.nombre.StartsWith(t, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(x => x.m.Codigo.EndsWith("001", StringComparison.Ordinal) ? 0 : 1) // capitales (código terminado en 001) primero
            .ThenBy(x => x.nombre, StringComparer.Ordinal)
            .Take(Math.Clamp(maximo, 1, 50))
            .Select(x => ADto(x.m))
            .ToList();
    }

    public MunicipioDto Obtener(string codigo)
        => ADto(Divipola.ObtenerMunicipio(codigo) ?? throw new NoEncontradoException("Municipio no encontrado."));
}
