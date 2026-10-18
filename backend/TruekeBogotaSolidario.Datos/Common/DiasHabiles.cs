namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Días hábiles en Colombia: de lunes a viernes, sin los festivos nacionales (Ley 51 de 1983, "ley Emiliani", y los que
/// dependen de la Pascua). Los plazos de ley (PQRS, habeas data, retracto) se cuentan en días hábiles.
/// </summary>
public static class DiasHabiles
{
    /// <summary>Colombia está en UTC-5 todo el año (no hay horario de verano).</summary>
    private static readonly TimeSpan ZonaColombia = TimeSpan.FromHours(-5);

    /// <summary>Festivos nacionales de un año, ya trasladados al lunes cuando corresponde.</summary>
    public static IReadOnlySet<DateOnly> Festivos(int anio)
    {
        var pascua = DomingoDePascua(anio);
        return new HashSet<DateOnly>
        {
            // Fijos: no se trasladan
            new(anio, 1, 1),   // Año Nuevo
            new(anio, 5, 1),   // Día del Trabajo
            new(anio, 7, 20),  // Independencia
            new(anio, 8, 7),   // Batalla de Boyacá
            new(anio, 12, 8),  // Inmaculada Concepción
            new(anio, 12, 25), // Navidad
            // Ley Emiliani: se pasan al lunes siguiente si no caen en lunes
            AlLunes(new(anio, 1, 6)),   // Reyes Magos
            AlLunes(new(anio, 3, 19)),  // San José
            AlLunes(new(anio, 6, 29)),  // San Pedro y San Pablo
            AlLunes(new(anio, 8, 15)),  // Asunción de la Virgen
            AlLunes(new(anio, 10, 12)), // Día de la Raza
            AlLunes(new(anio, 11, 1)),  // Todos los Santos
            AlLunes(new(anio, 11, 11)), // Independencia de Cartagena
            // Dependen de la Pascua
            pascua.AddDays(-3), // Jueves Santo
            pascua.AddDays(-2), // Viernes Santo
            pascua.AddDays(43), // Ascensión del Señor (lunes)
            pascua.AddDays(64), // Corpus Christi (lunes)
            pascua.AddDays(71), // Sagrado Corazón (lunes)
        };
    }

    public static bool EsHabil(DateOnly dia)
        => dia.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !Festivos(dia.Year).Contains(dia);

    /// <summary>
    /// Suma días hábiles a un instante UTC contando en la fecha de Colombia: el plazo vence el último día hábil a la misma
    /// hora en que se radicó.
    /// </summary>
    public static DateTime Sumar(DateTime desdeUtc, int dias)
    {
        var local = desdeUtc + ZonaColombia;
        var dia = DateOnly.FromDateTime(local);
        var sumados = 0;
        while (sumados < dias)
        {
            dia = dia.AddDays(1);
            if (EsHabil(dia)) sumados++;
        }
        return DateTime.SpecifyKind(dia.ToDateTime(TimeOnly.FromDateTime(local)) - ZonaColombia, DateTimeKind.Utc);
    }

    private static DateOnly AlLunes(DateOnly d) => d.DayOfWeek == DayOfWeek.Monday ? d : d.AddDays(((int)DayOfWeek.Monday - (int)d.DayOfWeek + 7) % 7);

    /// <summary>Domingo de Pascua (algoritmo de Meeus/Jones/Butcher, calendario gregoriano).</summary>
    public static DateOnly DomingoDePascua(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(anio, mes, dia);
    }
}
