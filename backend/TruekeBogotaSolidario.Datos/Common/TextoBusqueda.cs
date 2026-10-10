using System.Globalization;
using System.Text;

namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Búsqueda del catálogo sin servicios externos: cada publicación guarda su texto normalizado (minúsculas, sin tildes,
/// solo letras y números separados por un espacio) y la consulta se parte en términos que deben aparecer TODOS como
/// inicio de palabra. Así "camara" encuentra "Cámara", "bicicletas rojas" encuentra "Bicicleta roja de montaña" y
/// "sol" no encuentra "consola". Funciona igual en SQL Server (LIKE) y en InMemory (pruebas).
/// </summary>
public static class TextoBusqueda
{
    public const int MaxTerminos = 6;
    public const int LongitudMaxima = 2600;

    /// <summary>Palabras que no aportan a la búsqueda (artículos, preposiciones, conectores).</summary>
    private static readonly HashSet<string> Vacias = new(StringComparer.Ordinal)
    {
        "a", "al", "con", "de", "del", "el", "en", "es", "la", "las", "lo", "los", "o", "para", "por", "se", "sin", "su",
        "un", "una", "unas", "unos", "y"
    };

    /// <summary>" palabra1 palabra2 … " en minúsculas y sin tildes (con espacio al inicio y al final).</summary>
    public static string Normalizar(params string?[] partes)
    {
        var sb = new StringBuilder(" ");
        foreach (var parte in partes)
        {
            if (string.IsNullOrEmpty(parte)) continue;
            foreach (var c in parte.Normalize(NormalizationForm.FormD))
            {
                var categoria = CharUnicodeInfo.GetUnicodeCategory(c);
                if (categoria == UnicodeCategory.NonSpacingMark) continue; // tildes y diéresis (la ñ queda como n)
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
                else if (sb[^1] != ' ') sb.Append(' ');
            }
            if (sb[^1] != ' ') sb.Append(' ');
        }
        var texto = sb.ToString();
        return texto.Length <= LongitudMaxima ? texto : texto[..LongitudMaxima];
    }

    /// <summary>Términos de la consulta: normalizados, sin palabras vacías, en singular, sin repetir y como máximo 6.</summary>
    public static IReadOnlyList<string> Terminos(string? consulta)
    {
        if (string.IsNullOrWhiteSpace(consulta)) return Array.Empty<string>();
        return Normalizar(consulta).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !Vacias.Contains(p))
            .Select(Singular)
            .Where(p => p.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTerminos)
            .ToList();
    }

    /// <summary>
    /// Plural simple del español: "lapices" → "lapiz", "pantalones" → "pantalon", "zapatos" → "zapato".
    /// Como se busca por inicio de palabra, el singular también encuentra el plural guardado.
    /// </summary>
    public static string Singular(string palabra)
    {
        if (palabra.Length <= 3 || palabra.Any(char.IsDigit)) return palabra;
        if (palabra.EndsWith("ces", StringComparison.Ordinal)) return palabra[..^3] + "z";
        if (palabra.Length > 4 && palabra.EndsWith("es", StringComparison.Ordinal) && "lnrdj".Contains(palabra[^3]))
            return palabra[..^2];
        if (palabra.EndsWith('s') && !palabra.EndsWith("ss", StringComparison.Ordinal)) return palabra[..^1];
        return palabra;
    }
}
