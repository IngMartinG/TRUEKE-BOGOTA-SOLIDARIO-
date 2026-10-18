using System.Text.RegularExpressions;

namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Catálogo único de formatos de los datos de personas y empresas. Las constantes sirven tanto para los
/// <c>[RegularExpression]</c> de los DTO (error por campo en la API) como para las entidades (nada entra por otra vía).
/// Se toleran espacios al inicio y al final y espacios repetidos: el dominio recorta y deja uno solo.
/// El front repite las mismas reglas en <c>shared/validadores.ts</c>.
/// </summary>
public static class FormatosTexto
{
    /// <summary>Una palabra de un nombre: letras (con tildes, ñ, diéresis) con apóstrofo, guion o punto internos ("O'Neill", "Jean-Paul", "J.").</summary>
    private const string Palabra = @"\p{L}\p{M}*(?:['’.-]?\p{L}\p{M}*)*\.?";

    /// <summary>Nombre de persona: solo letras, espacio, apóstrofo, guion y punto. Sin dígitos ni separadores repetidos.</summary>
    public const string NombrePersona = "^ *" + Palabra + "(?: +" + Palabra + ")* *$";

    /// <summary>Nombre completo (registro y perfil): como <see cref="NombrePersona"/>, con al menos dos palabras.</summary>
    public const string NombreCompleto = "^ *" + Palabra + "(?: +" + Palabra + ")+ *$";

    /// <summary>Nombre comercial o razón social: letras, dígitos, espacio y &amp; . , - ' ( ). Al menos una letra o dígito ("3M").</summary>
    public const string NombreComercial = @"^(?=.*[\p{L}0-9]) *[\p{L}\p{M}0-9&.,'’()-]+(?: +[\p{L}\p{M}0-9&.,'’()-]+)* *$";

    /// <summary>Localidad o barrio: letras, dígitos, espacio y # - . °. Al menos una letra ("20 de Julio").</summary>
    public const string Localidad = @"^(?=.*\p{L}) *[\p{L}\p{M}0-9#.°º-]+(?: +[\p{L}\p{M}0-9#.°º-]+)* *$";

    /// <summary>NIT: solo dígitos y puntos, con el dígito de verificación opcional tras un guion. El DV lo revisa <see cref="DocumentosFiscales.NormalizarNit"/>.</summary>
    public const string Nit = @"^ *[0-9][0-9. ]*(?:- *[0-9])? *$";

    /// <summary>Dirección: letras, dígitos, espacio y # - . , ° /. Al menos una letra ("Calle 10 # 20-30, apto 301").</summary>
    public const string Direccion = @"^(?=.*\p{L}) *[\p{L}\p{M}0-9#.,°º/-]+(?: +[\p{L}\p{M}0-9#.,°º/-]+)* *$";

    public const string MensajeNombrePersona = "El nombre solo puede tener letras, espacios, apóstrofo, guion y punto.";
    public const string MensajeNombreCompleto = "Escribe tu nombre y apellido, solo con letras (se permiten espacios, apóstrofo, guion y punto).";
    public const string MensajeNombreComercial = "El nombre comercial solo puede tener letras, números, espacios y & . , - ' ( ).";
    public const string MensajeLocalidad = "La localidad o barrio solo puede tener letras, números, espacios y # - . °.";
    public const string MensajeRazonSocial = "La razón social solo puede tener letras, números, espacios y & . , - ' ( ).";
    public const string MensajeNit = "El NIT solo puede tener números, puntos y el dígito de verificación tras un guion.";
    public const string MensajeDireccion = "La dirección solo puede tener letras, números, espacios y # - . , ° /.";

    private static readonly TimeSpan Limite = TimeSpan.FromMilliseconds(250);
    private static readonly Regex ReNombrePersona = new(NombrePersona, RegexOptions.CultureInvariant, Limite);
    private static readonly Regex ReNombreCompleto = new(NombreCompleto, RegexOptions.CultureInvariant, Limite);
    private static readonly Regex ReNombreComercial = new(NombreComercial, RegexOptions.CultureInvariant, Limite);
    private static readonly Regex ReLocalidad = new(Localidad, RegexOptions.CultureInvariant, Limite);
    private static readonly Regex ReDireccion = new(Direccion, RegexOptions.CultureInvariant, Limite);
    private static readonly Regex Espacios = new(" {2,}", RegexOptions.CultureInvariant, Limite);

    public static bool EsNombrePersona(string? texto) => Cumple(ReNombrePersona, texto);
    public static bool EsNombreCompleto(string? texto) => Cumple(ReNombreCompleto, texto);
    public static bool EsNombreComercial(string? texto) => Cumple(ReNombreComercial, texto);
    public static bool EsLocalidad(string? texto) => Cumple(ReLocalidad, texto);
    public static bool EsDireccion(string? texto) => Cumple(ReDireccion, texto);

    /// <summary>Partículas que van en minúscula dentro de un nombre ("Ana de la Torre").</summary>
    private static readonly HashSet<string> Particulas = new(StringComparer.Ordinal) { "de", "del", "la", "las", "los", "y", "e", "da", "do", "dos", "van", "von" };

    /// <summary>
    /// Escribe bien un nombre que llega de afuera (Google): quita lo que no es letra ni separador de nombre (dígitos, emojis,
    /// guiones bajos…), deja un solo espacio y pone mayúscula inicial: "MARÍA JOSÉ O'NEILL" → "María José O'Neill",
    /// "ana de la torre" → "Ana de la Torre". Devuelve null si no queda un nombre de persona válido.
    /// </summary>
    public static string? EscribirNombre(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var limpio = new System.Text.StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(System.Text.NormalizationForm.FormC))
            limpio.Append(char.IsLetter(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark
                || c is '\'' or '’' or '.' or '-' ? c : ' ');
        var palabras = Compactar(limpio.ToString()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < palabras.Length; i++)
        {
            var p = palabras[i].ToLowerInvariant();
            palabras[i] = i > 0 && Particulas.Contains(p) ? p : Capitalizar(p);
        }
        var nombre = string.Join(' ', palabras);
        return nombre.Length is >= 2 and <= 120 && EsNombrePersona(nombre) ? nombre : null;
    }

    /// <summary>Mayúscula al inicio y después de apóstrofo, guion o punto ("o'neill" → "O'Neill", "jean-paul" → "Jean-Paul").</summary>
    private static string Capitalizar(string palabra)
    {
        var letras = palabra.ToCharArray();
        var inicio = true;
        for (var i = 0; i < letras.Length; i++)
        {
            if (inicio && char.IsLetter(letras[i])) { letras[i] = char.ToUpperInvariant(letras[i]); inicio = false; }
            else if (letras[i] is '\'' or '’' or '-' or '.') inicio = true;
        }
        return new string(letras);
    }

    /// <summary>Recorta y deja un solo espacio entre palabras.</summary>
    public static string Compactar(string? texto) => Espacios.Replace((texto ?? "").Trim(), " ");

    private static bool Cumple(Regex re, string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return false;
        try { return re.IsMatch(texto); }
        catch (RegexMatchTimeoutException) { return false; }
    }
}
