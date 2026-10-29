using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Negocio.Moderacion;

/// <summary>Revisión previa por riesgo (sección "Moderacion").</summary>
public sealed class ModeracionOpciones
{
    public const string Seccion = "Moderacion";
    /// <summary>false = todo se publica al instante (nada espera revisión, ni siquiera con denuncias).</summary>
    public bool PreAprobacion { get; set; } = true;
    /// <summary>Desde este puntaje la publicación espera a un moderador.</summary>
    [Range(1, 1000)] public int UmbralRiesgo { get; set; } = 50;
    /// <summary>Las primeras N publicaciones de cada cuenta siempre pasan por revisión.</summary>
    [Range(0, 20)] public int PrimerasRevisadas { get; set; } = 3;
    [Range(0, 90)] public int DiasCuentaNueva { get; set; } = 7;
    /// <summary>Personas distintas que denuncian una publicación para sacarla del catálogo hasta revisarla.</summary>
    [Range(2, 50)] public int DenunciantesParaRevisar { get; set; } = 3;
    /// <summary>Palabras o frases que se suman a la lista base (<see cref="EvaluadorRiesgo.ArticulosRestringidosBase"/>).</summary>
    public List<string> ArticulosRestringidos { get; set; } = [];
}

/// <summary>Lo que se sabe de quien publica (lo arma el servicio con datos de la base).</summary>
/// <param name="EsNueva">true al crear; al editar no cuenta la regla de "primeras publicaciones" (ya se revisaron).</param>
public sealed record ContextoRiesgo(DateTime FechaRegistroUtc, bool IdentidadVerificada, EstadoFotoPerfil EstadoFoto,
    int PublicacionesAprobadas, int DenunciasConfirmadas, bool EsNueva);

public sealed record EvaluacionRiesgo(int Puntaje, IReadOnlyList<string> Motivos, bool RequiereRevision);

/// <summary>
/// Decide si una publicación entra directo al catálogo o espera a un moderador. Es una suma de señales simples y
/// explicables (cada una deja su motivo para quien revisa); no adivina intenciones ni lee las fotos.
/// </summary>
public static partial class EvaluadorRiesgo
{
    public const int PesoPrimeras = 100;
    public const int PesoArticuloRestringido = 100;
    public const int PesoContacto = 60;
    public const int PesoDenuncias = 60;
    public const int PesoFueraDePlataforma = 30;
    public const int PesoCuentaNueva = 30;
    public const int PesoFotoEnRevision = 30;
    public const int PesoSinIdentidad = 10;

    /// <summary>
    /// Tomada de "Publicaciones prohibidas" de los términos. Se compara por palabra o frase completa, sin tildes. Se evitan
    /// palabras con usos comunes legítimos ("se arma fácil", "pistola de silicona", "pilas AAA"): solo mandan a revisión.
    /// </summary>
    public static readonly IReadOnlyList<string> ArticulosRestringidosBase =
    [
        // armas, municiones y explosivos
        "arma de fuego", "armas de fuego", "arma blanca", "armas blancas", "revolver", "escopeta", "municion", "municiones",
        "explosivo", "explosivos", "polvora", "pirotecnia", "taser",
        // medicamentos, sustancias y productos médicos con fórmula
        "droga", "drogas", "marihuana", "cannabis", "cocaina", "popper", "medicamento", "medicamentos", "antibiotico",
        "antibioticos", "tramadol", "clonazepam", "rivotril", "viagra", "esteroides", "anabolicos",
        // animales vivos
        "cachorro", "cachorros", "gatitos", "perritos", "pollitos", "animal vivo", "animales vivos",
        // falsificados
        "replica", "replicas", "imitacion",
    ];

    public static EvaluacionRiesgo Evaluar(string titulo, string descripcion, string? detalleCondicion, ContextoRiesgo c,
        ModeracionOpciones o, DateTime ahoraUtc)
    {
        var motivos = new List<string>();
        var puntaje = 0;
        void Sumar(int peso, string motivo) { puntaje += peso; motivos.Add(motivo); }

        if (c.EsNueva && c.PublicacionesAprobadas < o.PrimerasRevisadas)
            Sumar(PesoPrimeras, $"Una de sus primeras {o.PrimerasRevisadas} publicaciones");

        var crudo = $"{titulo}\n{descripcion}\n{detalleCondicion}";
        var normal = TextoBusqueda.Normalizar(titulo, descripcion, detalleCondicion);
        var restringidas = ArticulosRestringidosBase.Concat(o.ArticulosRestringidos)
            .Select(p => TextoBusqueda.Normalizar(p)).Where(p => p.Trim().Length > 0).Distinct(StringComparer.Ordinal)
            .Where(normal.Contains).Select(p => p.Trim()).Take(3).ToList();
        if (restringidas.Count > 0) Sumar(PesoArticuloRestringido, $"Posible artículo prohibido ({string.Join(", ", restringidas)})");

        if (Correo().IsMatch(crudo) || Enlace().IsMatch(crudo) || Telefono().IsMatch(crudo))
            Sumar(PesoContacto, "Datos de contacto o enlaces en el texto");
        else if (FueraDePlataforma().IsMatch(normal))
            Sumar(PesoFueraDePlataforma, "Invita a seguir la conversación fuera de Trueke");

        if (c.DenunciasConfirmadas > 0)
            Sumar(PesoDenuncias, c.DenunciasConfirmadas == 1 ? "Ya le ocultaron contenido por una denuncia"
                : $"Ya le ocultaron contenido por {c.DenunciasConfirmadas} denuncias");
        if (c.FechaRegistroUtc > ahoraUtc.AddDays(-o.DiasCuentaNueva)) Sumar(PesoCuentaNueva, $"Cuenta de menos de {o.DiasCuentaNueva} días");
        if (c.EstadoFoto is EstadoFotoPerfil.EnRevision or EstadoFotoPerfil.Rechazada) Sumar(PesoFotoEnRevision, "Su foto de perfil está en revisión");
        if (!c.IdentidadVerificada) Sumar(PesoSinIdentidad, "Identidad sin verificar");

        return new EvaluacionRiesgo(puntaje, motivos, o.PreAprobacion && puntaje >= o.UmbralRiesgo);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Correo();

    [GeneratedRegex(@"https?://|www\.|\b(?:wa\.me|t\.me|bit\.ly|linktr\.ee)\b|\b[a-z0-9-]{2,}\.(?:com|co|net|org|me|io|store|shop)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Enlace();

    /// <summary>Celular colombiano (3xx xxx xxxx, con o sin +57) o fijo (60x xxx xxxx). Un precio como 1.500.000 no coincide.</summary>
    [GeneratedRegex(@"(?<!\d)(?:\+?\s*57[\s.-]*)?(?:3\d{2}|60\d)[\s.-]*\d{3}[\s.-]*\d{2}[\s.-]*\d{2}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex Telefono();

    /// <summary>Medios de pago como Nequi no cuentan: una venta se paga por fuera de la plataforma.</summary>
    [GeneratedRegex(@" (?:whatsapp|wasap|wsp|whasap|telegram|instagram) ", RegexOptions.CultureInvariant)]
    private static partial Regex FueraDePlataforma();
}
