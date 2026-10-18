using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Los plazos de ley se cuentan en días hábiles colombianos: sin fines de semana ni festivos nacionales.</summary>
public class DiasHabilesTests
{
    [Theory]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2025, 4, 20)]
    public void Calcula_el_domingo_de_Pascua(int anio, int mes, int dia)
        => Assert.Equal(new DateOnly(anio, mes, dia), DiasHabiles.DomingoDePascua(anio));

    [Fact]
    public void Festivos_de_2026_con_ley_Emiliani_y_los_de_Pascua()
    {
        var esperados = new[]
        {
            "2026-01-01", "2026-01-12", "2026-03-23", "2026-04-02", "2026-04-03", "2026-05-01", "2026-05-18", "2026-06-08",
            "2026-06-15", "2026-06-29", "2026-07-20", "2026-08-07", "2026-08-17", "2026-10-12", "2026-11-02", "2026-11-16",
            "2026-12-08", "2026-12-25"
        }.Select(DateOnly.Parse).OrderBy(d => d);
        Assert.Equal(esperados, DiasHabiles.Festivos(2026).OrderBy(d => d));
    }

    [Fact]
    public void Salta_fines_de_semana_y_festivos()
    {
        // Viernes 30 de octubre de 2026, 3:00 p. m. en Colombia. Lunes 2 de noviembre (Todos los Santos) es festivo.
        var radicada = new DateTime(2026, 10, 30, 20, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 11, 3, 20, 0, 0, DateTimeKind.Utc), DiasHabiles.Sumar(radicada, 1));
        // 10 días hábiles: también se salta el lunes 16 (Independencia de Cartagena)
        Assert.Equal(new DateTime(2026, 11, 17, 20, 0, 0, DateTimeKind.Utc), DiasHabiles.Sumar(radicada, 10));
    }

    [Fact]
    public void Cuenta_en_la_fecha_de_Colombia_y_no_en_la_UTC()
    {
        // Jueves 1 de abril de 2026, 9:00 p. m. en Colombia (ya es viernes 2 en UTC). Jueves y Viernes Santo son festivos.
        var radicada = new DateTime(2026, 4, 2, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 4, 7, 2, 0, 0, DateTimeKind.Utc), DiasHabiles.Sumar(radicada, 1)); // lunes 6, 9:00 p. m.
    }

    [Fact]
    public void La_consulta_de_datos_se_responde_en_10_dias_habiles_y_lo_demas_en_15()
    {
        var ahora = new DateTime(2026, 10, 30, 20, 0, 0, DateTimeKind.Utc);
        var consulta = Pqr.CrearPublica("Ana Pérez", "ana@t.co", TipoPqr.Consulta, "Qué datos tienen", "Quiero saber qué datos míos guardan.", ahora);
        var reclamo = Pqr.CrearPublica("Ana Pérez", "ana@t.co", TipoPqr.Reclamo, "Corregir mis datos", "Mi nombre quedó mal escrito en la cuenta.", ahora);
        Assert.Equal(DiasHabiles.Sumar(ahora, 10), consulta.FechaLimiteUtc);
        Assert.Equal(DiasHabiles.Sumar(ahora, 15), reclamo.FechaLimiteUtc);
    }

    [Fact]
    public void El_canal_publico_no_admite_retracto_ni_nombres_invalidos()
    {
        var ahora = DateTime.UtcNow;
        Assert.Throws<ReglaDeNegocioException>(() => Pqr.CrearPublica("Ana Pérez", "ana@t.co", TipoPqr.Retracto, "Quiero el retracto", "Pido el retracto de mi compra.", ahora));
        Assert.Throws<ReglaDeNegocioException>(() => Pqr.CrearPublica("Ana2", "ana@t.co", TipoPqr.Peticion, "Una petición", "Texto de la petición válido.", ahora));
    }
}
