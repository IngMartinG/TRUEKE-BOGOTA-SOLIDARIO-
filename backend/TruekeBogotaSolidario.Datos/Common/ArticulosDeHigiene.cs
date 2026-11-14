namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Artículos de higiene personal que, por salud, solo se pueden publicar como nuevos: ropa interior, medias y trajes de baño.
/// Se detectan por palabra o frase completa, sin tildes ni mayúsculas ("calzón" y "CALZON" son lo mismo; "calzado" no).
/// Si se agrega un término, también va en <c>frontend/src/app/shared/higiene.ts</c> (el front avisa antes de enviar).
/// </summary>
public static class ArticulosDeHigiene
{
    public const string Mensaje =
        "Por higiene, la ropa interior, las medias y los trajes de baño solo se publican como «Nuevo». " +
        "Si tu artículo no es de ese tipo, ajusta el título.";

    public static readonly IReadOnlyList<string> Terminos =
    [
        // ropa interior
        "ropa interior", "calzoncillo", "calzoncillos", "calzon", "calzones", "panty", "pantys", "panties", "pantaleta", "pantaletas",
        "brasier", "brasieres", "sosten", "sostenes", "boxer", "boxers", "tanga", "tangas", "lenceria", "lingerie",
        // medias
        "medias", "calcetin", "calcetines", "calceta", "calcetas",
        // trajes de baño
        "traje de bano", "trajes de bano", "vestido de bano", "vestidos de bano", "ropa de bano", "bikini", "bikinis",
        "banador", "banadores",
    ];

    private static readonly string[] Normalizados = Terminos.Select(t => TextoBusqueda.Normalizar(t)).ToArray();

    /// <summary>El primer término de higiene que aparece en los textos (normalizado), o null si no hay ninguno.</summary>
    public static string? Detectar(params string?[] textos)
    {
        var normal = TextoBusqueda.Normalizar(textos);
        for (var i = 0; i < Normalizados.Length; i++)
            if (normal.Contains(Normalizados[i], StringComparison.Ordinal)) return Terminos[i];
        return null;
    }
}
