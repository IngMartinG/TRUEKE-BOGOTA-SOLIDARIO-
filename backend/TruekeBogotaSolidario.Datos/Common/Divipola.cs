using System.Text;
using System.Text.Json;

namespace TruekeBogotaSolidario.Datos.Common;

public sealed record Departamento(string Codigo, string Nombre);

public sealed record Municipio(string Codigo, string DepartamentoCodigo, string Nombre, double Latitud, double Longitud);

/// <summary>
/// Catálogo oficial de departamentos y municipios de Colombia (DANE - DIVIPOLA), embebido en el ensamblado.
/// Es dato de referencia estático: no vive en la base de datos y se carga una sola vez.
/// El código de municipio tiene 5 dígitos y sus 2 primeros son el código del departamento (Bogotá = 11001).
/// </summary>
public static class Divipola
{
    public const string CodigoBogota = "11001";

    private sealed record Archivo(List<Departamento> Departamentos, List<Municipio> Municipios);

    private static readonly Lazy<(IReadOnlyList<Departamento> Deps, IReadOnlyDictionary<string, Municipio> Mpios)> Datos = new(Cargar);

    private static (IReadOnlyList<Departamento>, IReadOnlyDictionary<string, Municipio>) Cargar()
    {
        using var flujo = typeof(Divipola).Assembly.GetManifestResourceStream("TruekeBogotaSolidario.Datos.divipola.json")
                          ?? throw new InvalidOperationException("No se encontró el recurso divipola.json.");
        var archivo = JsonSerializer.Deserialize<Archivo>(flujo, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                      ?? throw new InvalidOperationException("divipola.json está vacío.");
        if (archivo.Municipios.Count < 1000 || archivo.Municipios.Any(m => m.DepartamentoCodigo is null || m.Codigo is null || m.Nombre is null)
            || archivo.Municipios.Any(m => !m.Codigo.StartsWith(m.DepartamentoCodigo, StringComparison.Ordinal)))
            throw new InvalidOperationException("divipola.json está incompleto o tiene códigos inconsistentes.");
        return (archivo.Departamentos.OrderBy(d => Normalizar(d.Nombre), StringComparer.Ordinal).ToList(),
            archivo.Municipios.ToDictionary(m => m.Codigo, StringComparer.Ordinal));
    }

    public static IReadOnlyList<Departamento> Departamentos => Datos.Value.Deps;

    public static IEnumerable<Municipio> Municipios => Datos.Value.Mpios.Values;

    public static Municipio? ObtenerMunicipio(string? codigo)
        => codigo is not null && Datos.Value.Mpios.TryGetValue(codigo.Trim(), out var m) ? m : null;

    public static Departamento? ObtenerDepartamento(string? codigo)
        => codigo is null ? null : Departamentos.FirstOrDefault(d => d.Codigo == codigo.Trim());

    public static IReadOnlyList<Municipio> MunicipiosDe(string departamentoCodigo)
        => Municipios.Where(m => m.DepartamentoCodigo == departamentoCodigo)
            .OrderBy(m => Normalizar(m.Nombre), StringComparer.Ordinal).ToList();

    /// <summary>Valida y devuelve el municipio; lanza ReglaDeNegocioException si el código no existe.</summary>
    public static Municipio Exigir(string? codigo)
        => ObtenerMunicipio(codigo) ?? throw new ReglaDeNegocioException("El municipio no es válido. Elige uno de la lista.");

    /// <summary>"Medellín, Antioquia" (Bogotá se muestra sin repetir el departamento).</summary>
    public static string NombreCompleto(string? codigo)
    {
        var m = ObtenerMunicipio(codigo);
        if (m is null) return "";
        if (m.Codigo == CodigoBogota) return "Bogotá, D.C.";
        var d = ObtenerDepartamento(m.DepartamentoCodigo);
        return d is null ? m.Nombre : $"{m.Nombre}, {d.Nombre}";
    }

    /// <summary>
    /// Para buscar y ordenar sin tildes ni mayúsculas ("bogota" encuentra "Bogotá, D.C."). Tabla explícita a propósito:
    /// la API corre con InvariantGlobalization (sin datos de cultura ni normalización Unicode en el contenedor).
    /// </summary>
    public static string Normalizar(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (var c in texto.Trim())
            sb.Append(c switch
            {
                'á' or 'à' or 'ä' or 'â' or 'Á' or 'À' or 'Ä' or 'Â' => 'a',
                'é' or 'è' or 'ë' or 'ê' or 'É' or 'È' or 'Ë' or 'Ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' or 'Í' or 'Ì' or 'Ï' or 'Î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' or 'Ó' or 'Ò' or 'Ö' or 'Ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' or 'Ú' or 'Ù' or 'Ü' or 'Û' => 'u',
                'ñ' or 'Ñ' => 'n',
                >= 'A' and <= 'Z' => (char)(c + 32),
                _ => c
            });
        return sb.ToString();
    }
}
