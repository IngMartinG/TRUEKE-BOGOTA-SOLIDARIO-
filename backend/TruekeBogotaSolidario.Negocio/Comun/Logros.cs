using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Lo que se necesita para evaluar los logros de una persona. Los intercambios por modo cuentan solo los que le dieron
/// Eco-Puntos: así el anti-farmeo (5 al día, misma pareja 1 vez cada 30 días) también protege las medallas.
/// </summary>
public sealed record DatosLogros(int Truekes, int Compras, int Donaciones, decimal? CalificacionPromedio, int TotalCalificaciones,
    int? TasaConcrecion, double? RespuestaHoras, DateTime FechaRegistroUtc);

/// <summary>
/// Medallas del perfil. Son solo un reconocimiento (decisión del dueño): no dan Eco-Puntos, permisos ni descuentos.
/// Se calculan al vuelo con hechos que el perfil público ya muestra, así que no exponen nada nuevo ni necesitan tabla.
/// </summary>
public static class Logros
{
    /// <summary>Metas de bronce, plata y oro de los logros por modo.</summary>
    public static readonly int[] MetasPorModo = [3, 10, 30];
    public const decimal PromedioBienCalificado = 4.5m;
    public const int MinimoCalificaciones = 5;
    public const int TasaCumplidor = 90;
    public const int MinimoIntercambiosCumplidor = 5;
    public const double HorasRespuestaRapida = 12;
    public const int MinimoIntercambiosRespuesta = 3;
    public const int DiasAnio = 365;
    /// <summary>Quien se registró antes de esta fecha es pionero. No se puede ganar después, así que nunca se muestra como pendiente.</summary>
    public static readonly DateTime FinEtapaPionera = new(2027, 1, 1, 5, 0, 0, DateTimeKind.Utc); // medianoche en Colombia

    private static readonly string[] Grados = ["Bronce", "Plata", "Oro"];

    /// <summary>Todos los logros con su progreso, en orden fijo (para "Tus logros").</summary>
    public static IReadOnlyList<LogroDto> Evaluar(DatosLogros d, DateTime ahoraUtc)
    {
        var total = d.Truekes + d.Compras + d.Donaciones;
        var lista = new List<LogroDto>
        {
            Simple("primer-intercambio", "Primer intercambio", "Completó su primer intercambio.", total, 1),
            PorGrados("truekero", "Truekero", "Truekes completados.", d.Truekes),
            PorGrados("corazon-solidario", "Corazón solidario", "Donaciones completadas.", d.Donaciones),
            PorGrados("segunda-mano", "Segunda mano", "Compras y ventas completadas.", d.Compras),
            // El avance es contar calificaciones; el promedio decide al llegar a la meta
            Condicion("bien-calificado", "Bien calificado", $"Promedio de {PromedioBienCalificado:0.0}★ o más con {MinimoCalificaciones} calificaciones.",
                d.TotalCalificaciones, MinimoCalificaciones,
                d.TotalCalificaciones >= MinimoCalificaciones && d.CalificacionPromedio >= PromedioBienCalificado),
            Condicion("cumplidor", "Cumplidor", $"Concreta el {TasaCumplidor} % o más de sus intercambios aceptados.",
                total, MinimoIntercambiosCumplidor, total >= MinimoIntercambiosCumplidor && d.TasaConcrecion >= TasaCumplidor),
            Condicion("responde-rapido", "Responde rápido", $"Acepta solicitudes en menos de {HorasRespuestaRapida:0} horas (mediana).",
                total, MinimoIntercambiosRespuesta, total >= MinimoIntercambiosRespuesta && d.RespuestaHoras <= HorasRespuestaRapida),
            Simple("un-anio", "Un año en la comunidad", "Lleva un año o más en Trueke.",
                (int)Math.Clamp((ahoraUtc - d.FechaRegistroUtc).TotalDays, 0, DiasAnio), DiasAnio),
        };
        if (d.FechaRegistroUtc < FinEtapaPionera)
            lista.Add(new LogroDto("pionero", "Vecino pionero", "Se unió en la primera etapa de la plataforma.", null, true, 1, 1));
        return lista;
    }

    /// <summary>Solo los obtenidos: lo que ve cualquiera en el perfil público.</summary>
    public static IReadOnlyList<LogroDto> Obtenidos(DatosLogros d, DateTime ahoraUtc)
        => Evaluar(d, ahoraUtc).Where(l => l.Obtenido).ToList();

    private static LogroDto Simple(string clave, string nombre, string descripcion, int actual, int meta)
        => new(clave, nombre, descripcion, null, actual >= meta, Math.Min(actual, meta), meta);

    private static LogroDto Condicion(string clave, string nombre, string descripcion, int actual, int meta, bool obtenido)
        => new(clave, nombre, descripcion, null, obtenido, Math.Min(actual, meta), meta);

    /// <summary>Grado = el más alto alcanzado; Meta = la del siguiente grado (o la del oro si ya lo tiene).</summary>
    private static LogroDto PorGrados(string clave, string nombre, string descripcion, int actual)
    {
        var alcanzados = MetasPorModo.Count(m => actual >= m);
        var grado = alcanzados > 0 ? Grados[alcanzados - 1] : null;
        var meta = MetasPorModo[Math.Min(alcanzados, MetasPorModo.Length - 1)];
        return new LogroDto(clave, nombre, descripcion, grado, alcanzados > 0, Math.Min(actual, meta), meta);
    }
}
