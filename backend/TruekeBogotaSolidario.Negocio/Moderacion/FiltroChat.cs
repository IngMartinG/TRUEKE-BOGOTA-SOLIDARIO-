using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Moderacion;

public enum DecisionFiltroChat { Normal, Marcar, Bloquear }

public sealed record ResultadoFiltroChat(DecisionFiltroChat Decision, string? Motivo)
{
    public static readonly ResultadoFiltroChat Normal = new(DecisionFiltroChat.Normal, null);
}

/// <summary>
/// Revisión automática de cada mensaje del chat, sin que nadie lo lea:
/// <list type="bullet">
/// <item>Bloquea lo que solo sirve para cuadrar algo ilícito (armas de fuego, explosivos, drogas, medicamentos controlados).</item>
/// <item>Marca, sin frenar el envío, lo que suele anteceder a una estafa o a un negocio por fuera: datos de contacto,
/// enlaces, invitaciones a WhatsApp o Telegram y palabras que en una charla pueden ser inocentes ("cachorro", "medicamento").
/// Un moderador puede leer el mensaje marcado junto con los de alrededor.</item>
/// </list>
/// Se compara por palabra o frase completa y sin tildes, igual que <see cref="EvaluadorRiesgo"/>.
/// </summary>
public static class FiltroChat
{
    public const string MensajeBloqueo =
        "Tu mensaje no se envió: menciona artículos prohibidos en Trueke (armas, explosivos, drogas o medicamentos controlados). "
        + "Si fue un error, escríbelo de otra forma.";

    /// <summary>
    /// Sin uso legítimo entre vecinos que intercambian objetos: el mensaje no se envía. Se dejan por fuera las palabras con
    /// otros sentidos comunes ("granada" es fruta y municipio, "revolver" es un verbo, "éxtasis" una emoción).
    /// </summary>
    public static readonly IReadOnlyList<string> Bloqueadas =
    [
        "arma de fuego", "armas de fuego", "municion", "municiones", "explosivo", "explosivos", "polvora",
        "marihuana", "cocaina", "bazuco", "tusi", "popper", "poppers", "lsd",
        "tramadol", "clonazepam", "rivotril", "fentanilo",
    ];

    /// <summary>Palabras que en una charla pueden ser inocentes ("droga" en "droguería" no cuenta): solo marcan.</summary>
    private static readonly IReadOnlyList<string> SoloMarcan = ["droga", "drogas"];

    /// <summary>De la lista de publicaciones, lo que en una charla casi siempre es otra cosa ("hay que revolver la mezcla").</summary>
    private static readonly HashSet<string> NoAplicanEnChat = new(StringComparer.Ordinal) { "revolver" };

    public static ResultadoFiltroChat Evaluar(string? texto, ModeracionOpciones opciones)
    {
        if (string.IsNullOrWhiteSpace(texto)) return ResultadoFiltroChat.Normal;
        var normal = TextoBusqueda.Normalizar(texto);

        var bloqueadas = Coincidencias(normal, Bloqueadas);
        if (bloqueadas.Count > 0) return new(DecisionFiltroChat.Bloquear, $"Artículo prohibido ({string.Join(", ", bloqueadas)})");

        var motivos = new List<string>();
        if (EvaluadorRiesgo.Telefono().IsMatch(texto)) motivos.Add("Comparte un teléfono");
        if (EvaluadorRiesgo.Correo().IsMatch(texto)) motivos.Add("Comparte un correo");
        if (EvaluadorRiesgo.Enlace().IsMatch(texto)) motivos.Add("Comparte un enlace");
        if (EvaluadorRiesgo.FueraDePlataforma().IsMatch(normal)) motivos.Add("Invita a seguir por fuera de Trueke");
        var sensibles = Coincidencias(normal, EvaluadorRiesgo.ArticulosRestringidosBase.Where(p => !NoAplicanEnChat.Contains(p))
            .Concat(SoloMarcan).Concat(opciones.ArticulosRestringidos));
        if (sensibles.Count > 0) motivos.Add($"Posible artículo prohibido ({string.Join(", ", sensibles)})");

        return motivos.Count == 0 ? ResultadoFiltroChat.Normal : new(DecisionFiltroChat.Marcar, string.Join("; ", motivos));
    }

    private static List<string> Coincidencias(string normal, IEnumerable<string> frases)
        => frases.Select(p => TextoBusqueda.Normalizar(p)).Where(p => p.Trim().Length > 0).Distinct(StringComparer.Ordinal)
            .Where(normal.Contains).Select(p => p.Trim()).Take(3).ToList();
}
