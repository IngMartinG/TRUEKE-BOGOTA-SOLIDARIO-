namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Filtro común de los textos libres (títulos, descripciones, comentarios, mensajes, denuncias, PQRS…): rechaza los
/// caracteres de control (salvo \n y \r en los textos de varias líneas) y los invisibles que sirven para engañar
/// (invertir el sentido del texto, esconder palabras o partir enlaces). El ZWJ (U+200D) se permite: lo usan los emojis.
/// </summary>
public static class TextoSeguro
{
    public const string Mensaje = "El texto contiene caracteres no permitidos.";

    public static bool EsSeguro(string? texto, bool multilinea = false)
    {
        if (texto is null) return true;
        foreach (var c in texto)
        {
            if (char.IsControl(c) && !(multilinea && c is '\n' or '\r')) return false;
            if (EsInvisibleDeEngano(c)) return false;
        }
        return true;
    }

    /// <summary>Lanza <see cref="ReglaDeNegocioException"/> con el mensaje dado si el texto no es seguro.</summary>
    public static void Exigir(string? texto, bool multilinea = false, string mensaje = Mensaje)
    {
        if (!EsSeguro(texto, multilinea)) throw new ReglaDeNegocioException(mensaje);
    }

    private static bool EsInvisibleDeEngano(char c) => c is
        >= '\u202A' and <= '\u202E' // incrustaciones y anulaciones de dirección (RLO, LRO…)
        or >= '\u2066' and <= '\u2069' // aislamientos de dirección
        or '\u200B' // espacio de ancho cero
        or '\uFEFF' // BOM / no separable de ancho cero
        or '\u2060' // unión de palabras
        or '\u180E'; // separador de vocales mongol
}
