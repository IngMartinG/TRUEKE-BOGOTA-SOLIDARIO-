namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Filtro común de los textos libres (títulos, descripciones, comentarios, mensajes, denuncias, PQRS…): rechaza los
/// caracteres de control (salvo \n y \r en los textos de varias líneas) y los invisibles que sirven para engañar
/// (invertir el sentido del texto, esconder palabras o partir enlaces). El ZWJ (U+200D) se permite: lo usan los emojis.
/// También rechaza lo que tiene forma de etiqueta HTML (ver <see cref="TieneEtiqueta"/>).
/// </summary>
public static class TextoSeguro
{
    public const string Mensaje = "El texto contiene caracteres no permitidos o etiquetas HTML.";

    public static bool EsSeguro(string? texto, bool multilinea = false)
    {
        if (texto is null) return true;
        foreach (var c in texto)
        {
            if (char.IsControl(c) && !(multilinea && c is '\n' or '\r')) return false;
            if (EsInvisibleDeEngano(c)) return false;
        }
        return !TieneEtiqueta(texto);
    }

    /// <summary>
    /// Un "&lt;" seguido de una letra, "/", "!" o "?": así empieza cualquier etiqueta, comentario o instrucción de HTML.
    /// Los textos son planos y no se guarda marcado; un "&lt;" suelto sigue siendo válido ("precio &lt; 50.000", "&lt;3").
    /// </summary>
    public static bool TieneEtiqueta(string texto)
    {
        for (var i = texto.IndexOf('<'); i >= 0 && i + 1 < texto.Length; i = texto.IndexOf('<', i + 1))
        {
            var siguiente = texto[i + 1];
            if (char.IsAsciiLetter(siguiente) || siguiente is '/' or '!' or '?') return true;
        }
        return false;
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
