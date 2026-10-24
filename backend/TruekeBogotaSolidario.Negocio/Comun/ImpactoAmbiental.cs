namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Kilos y CO₂ estimados de un objeto típico de una categoría.</summary>
public sealed record FactorImpacto(decimal KgPorObjeto, decimal KgCo2PorObjeto);

/// <summary>
/// Estimación del impacto ambiental de los intercambios: kilos que no van a la basura y CO₂ que se deja de emitir al no
/// fabricar un objeto nuevo. Son promedios por categoría, a la baja a propósito (del orden de los estudios de ciclo de vida
/// de WRAP y la EPA, recortados): es mejor quedarse corto que presumir. Se muestran siempre como estimación.
/// Cada intercambio cuenta un objeto (lo publicado), igual que "Objetos con nuevo hogar".
/// </summary>
public static class ImpactoAmbiental
{
    /// <summary>Por id de categoría (los de la semilla). Una categoría que no esté aquí usa la de "Otros".</summary>
    public static readonly IReadOnlyDictionary<int, FactorImpacto> PorCategoria = new Dictionary<int, FactorImpacto>
    {
        [1] = new(0.5m, 5m),   // Ropa y calzado
        [2] = new(0.4m, 1m),   // Libros y papelería
        [3] = new(6m, 30m),    // Electrodomésticos
        [4] = new(8m, 20m),    // Hogar y muebles
        [5] = new(1m, 40m),    // Tecnología
        [6] = new(0.8m, 3m),   // Juguetes y niños
        [7] = new(1m, 2m),     // Otros
    };

    public static FactorImpacto Factor(int categoriaId) => PorCategoria.GetValueOrDefault(categoriaId) ?? PorCategoria[7];

    /// <summary>Suma los objetos por categoría; kilos y CO₂ con un decimal.</summary>
    public static (int Objetos, decimal Kg, decimal KgCo2) Estimar(IReadOnlyDictionary<int, int> objetosPorCategoria)
    {
        decimal kg = 0, co2 = 0;
        foreach (var (categoria, cantidad) in objetosPorCategoria)
        {
            var f = Factor(categoria);
            kg += f.KgPorObjeto * cantidad;
            co2 += f.KgCo2PorObjeto * cantidad;
        }
        return (objetosPorCategoria.Values.Sum(), Math.Round(kg, 1), Math.Round(co2, 1));
    }
}
