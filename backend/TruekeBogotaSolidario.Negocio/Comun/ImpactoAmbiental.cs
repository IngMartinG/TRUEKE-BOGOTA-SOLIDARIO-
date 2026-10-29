using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Peso y material de un objeto típico de una categoría (cuando quien publica no los declara).</summary>
public sealed record TipicoCategoria(decimal Kg, MaterialObjeto Material);

/// <summary>Ayuda al publicar: elegir "una silla de oficina" llena el peso y el material.</summary>
public sealed record ObjetoTipico(string Nombre, decimal Kg, MaterialObjeto Material, int CategoriaId);

/// <summary>
/// Metodología de impacto 2.0.0 (docs/METODOLOGIA-IMPACTO.md): cada objeto entregado en un intercambio que ambas partes
/// confirmaron suma sus kilos (los declarados o, si faltan, los típicos de su categoría), y el CO₂ evitado es
/// kilos × factor de su material. Los factores viven en la base de datos; cada registro guarda el que usó.
/// </summary>
public static class ImpactoAmbiental
{
    /// <summary>Por id de categoría (los de la semilla). Una categoría que no esté aquí usa la de "Otros".</summary>
    public static readonly IReadOnlyDictionary<int, TipicoCategoria> PorCategoria = new Dictionary<int, TipicoCategoria>
    {
        [1] = new(0.5m, MaterialObjeto.Textil),           // Ropa y calzado
        [2] = new(0.4m, MaterialObjeto.Papel),            // Libros y papelería
        [3] = new(6m, MaterialObjeto.Electrodomestico),   // Electrodomésticos
        [4] = new(8m, MaterialObjeto.Mixto),              // Hogar y muebles
        [5] = new(1m, MaterialObjeto.Electronica),        // Tecnología
        [6] = new(0.8m, MaterialObjeto.Plastico),         // Juguetes y niños
        [7] = new(1m, MaterialObjeto.Mixto),              // Otros
    };

    public static TipicoCategoria Tipico(int categoriaId) => PorCategoria.GetValueOrDefault(categoriaId) ?? PorCategoria[7];

    public static readonly IReadOnlyList<ObjetoTipico> ObjetosTipicos =
    [
        new("Prenda de ropa", 0.4m, MaterialObjeto.Textil, 1),
        new("Par de zapatos", 0.8m, MaterialObjeto.Mixto, 1),
        new("Libro", 0.5m, MaterialObjeto.Papel, 2),
        new("Caja de 20 libros", 10m, MaterialObjeto.Papel, 2),
        new("Nevera", 55m, MaterialObjeto.Electrodomestico, 3),
        new("Lavadora", 60m, MaterialObjeto.Electrodomestico, 3),
        new("Microondas", 12m, MaterialObjeto.Electrodomestico, 3),
        new("Ventilador", 4m, MaterialObjeto.Electrodomestico, 3),
        new("Licuadora", 3m, MaterialObjeto.Electrodomestico, 3),
        new("Plancha", 1.5m, MaterialObjeto.Electrodomestico, 3),
        new("Colchón sencillo", 18m, MaterialObjeto.Textil, 4),
        new("Silla de oficina", 8m, MaterialObjeto.Mixto, 4),
        new("Silla plástica", 3m, MaterialObjeto.Plastico, 4),
        new("Mesa pequeña", 12m, MaterialObjeto.Madera, 4),
        new("Sofá de dos puestos", 35m, MaterialObjeto.Mixto, 4),
        new("Juego de ollas", 5m, MaterialObjeto.Metal, 4),
        new("Televisor de 32\"", 6m, MaterialObjeto.Electronica, 5),
        new("Monitor", 3.5m, MaterialObjeto.Electronica, 5),
        new("Portátil", 2.2m, MaterialObjeto.Electronica, 5),
        new("Teclado", 0.8m, MaterialObjeto.Electronica, 5),
        new("Mouse", 0.1m, MaterialObjeto.Electronica, 5),
        new("Impresora", 6m, MaterialObjeto.Electronica, 5),
        new("Coche de bebé", 9m, MaterialObjeto.Mixto, 6),
        new("Bolsa de juguetes", 4m, MaterialObjeto.Plastico, 6),
        new("Bicicleta", 14m, MaterialObjeto.Metal, 7),
        new("Bicicleta estática", 25m, MaterialObjeto.Metal, 7),
    ];

    /// <summary>Peso y material con que cuenta un objeto: lo declarado o, si falta, lo típico de su categoría.</summary>
    public static (decimal Kg, OrigenPeso Origen, MaterialObjeto Material) PesoYMaterial(Publicacion p)
    {
        var tipico = Tipico(p.CategoriaId);
        return p.PesoEstimadoKg is { } kg
            ? (kg, OrigenPeso.Declarado, p.Material ?? tipico.Material)
            : (tipico.Kg, OrigenPeso.EstimadoPorCategoria, p.Material ?? tipico.Material);
    }

    /// <summary>
    /// Un registro por objeto entregado: lo publicado va del dueño a quien lo pidió y, en un trueke con objetos, los ofrecidos
    /// van en sentido contrario.
    /// </summary>
    public static IEnumerable<RegistroImpacto> Registros(Transaccion t, Publicacion pedida, IEnumerable<Publicacion> ofrecidas,
        IReadOnlyDictionary<MaterialObjeto, FactorImpacto> factores)
    {
        foreach (var (objeto, de, para) in ofrecidas.Select(o => (o, t.ReceptorId, t.OferenteId)).Prepend((pedida, t.OferenteId, t.ReceptorId)))
        {
            var (kg, origen, material) = PesoYMaterial(objeto);
            var factor = factores.GetValueOrDefault(material) ?? factores[MaterialObjeto.Mixto];
            yield return new RegistroImpacto(t.Id, objeto, t.Modo, de, para, kg, origen, material, factor, t.FechaUtc);
        }
    }
}
