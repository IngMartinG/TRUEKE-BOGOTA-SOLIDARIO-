using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class LogrosTests
{
    private static readonly DateTime Ahora = new(2027, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RegistroReciente = new(2027, 5, 1, 12, 0, 0, DateTimeKind.Utc); // después de la etapa pionera

    private static DatosLogros Datos(int truekes = 0, int compras = 0, int donaciones = 0, decimal? promedio = null, int calificaciones = 0,
        int? tasa = null, double? respuesta = null, DateTime? registro = null)
        => new(truekes, compras, donaciones, promedio, calificaciones, tasa, respuesta, registro ?? RegistroReciente);

    private static LogroDto Logro(DatosLogros d, string clave) => Logros.Evaluar(d, Ahora).Single(l => l.Clave == clave);

    [Fact]
    public void Cuenta_nueva_no_tiene_ningun_logro_y_ve_su_progreso()
    {
        var todos = Logros.Evaluar(Datos(), Ahora);
        Assert.All(todos, l => Assert.False(l.Obtenido));
        Assert.Empty(Logros.Obtenidos(Datos(), Ahora));
        Assert.Equal(new[] { "primer-intercambio", "truekero", "corazon-solidario", "segunda-mano", "bien-calificado",
            "cumplidor", "responde-rapido", "un-anio" }, todos.Select(l => l.Clave));
    }

    [Theory]
    [InlineData(0, null, 0, 3)]
    [InlineData(2, null, 2, 3)]
    [InlineData(3, "Bronce", 3, 10)]
    [InlineData(10, "Plata", 10, 30)]
    [InlineData(29, "Plata", 29, 30)]
    [InlineData(30, "Oro", 30, 30)]
    [InlineData(80, "Oro", 30, 30)]
    public void Los_logros_por_modo_suben_de_grado(int donaciones, string? grado, int progreso, int meta)
    {
        var l = Logro(Datos(donaciones: donaciones), "corazon-solidario");
        Assert.Equal(grado, l.Grado);
        Assert.Equal(grado is not null, l.Obtenido);
        Assert.Equal(progreso, l.Progreso);
        Assert.Equal(meta, l.Meta);
    }

    [Fact]
    public void Primer_intercambio_cuenta_cualquier_modo()
    {
        Assert.True(Logro(Datos(compras: 1), "primer-intercambio").Obtenido);
        Assert.Null(Logro(Datos(compras: 1), "primer-intercambio").Grado);
    }

    [Fact]
    public void Bien_calificado_exige_promedio_y_minimo_de_calificaciones()
    {
        Assert.False(Logro(Datos(promedio: 5m, calificaciones: 4), "bien-calificado").Obtenido); // muy pocas
        Assert.False(Logro(Datos(promedio: 4.4m, calificaciones: 20), "bien-calificado").Obtenido);
        Assert.True(Logro(Datos(promedio: 4.5m, calificaciones: 5), "bien-calificado").Obtenido);
        Assert.Equal(4, Logro(Datos(promedio: 5m, calificaciones: 4), "bien-calificado").Progreso);
    }

    [Fact]
    public void Cumplidor_y_responde_rapido_necesitan_historial_suficiente()
    {
        Assert.False(Logro(Datos(truekes: 4, tasa: 100), "cumplidor").Obtenido);
        Assert.False(Logro(Datos(truekes: 5, tasa: 89), "cumplidor").Obtenido);
        Assert.False(Logro(Datos(truekes: 5), "cumplidor").Obtenido); // sin tasa
        Assert.True(Logro(Datos(truekes: 5, tasa: 90), "cumplidor").Obtenido);

        Assert.False(Logro(Datos(truekes: 2, respuesta: 1), "responde-rapido").Obtenido);
        Assert.False(Logro(Datos(truekes: 3, respuesta: 12.5), "responde-rapido").Obtenido);
        Assert.True(Logro(Datos(truekes: 3, respuesta: 12), "responde-rapido").Obtenido);
    }

    [Fact]
    public void Un_anio_en_la_comunidad_muestra_los_dias()
    {
        var medio = Logro(Datos(registro: Ahora.AddDays(-100)), "un-anio");
        Assert.False(medio.Obtenido);
        Assert.Equal(100, medio.Progreso);
        Assert.True(Logro(Datos(registro: Ahora.AddDays(-365)), "un-anio").Obtenido);
    }

    [Fact]
    public void Pionero_solo_para_quien_se_registro_en_la_primera_etapa_y_nunca_como_pendiente()
    {
        var pionero = Logros.Evaluar(Datos(registro: Logros.FinEtapaPionera.AddSeconds(-1)), Ahora).Single(l => l.Clave == "pionero");
        Assert.True(pionero.Obtenido);
        Assert.DoesNotContain(Logros.Evaluar(Datos(registro: Logros.FinEtapaPionera), Ahora), l => l.Clave == "pionero");
    }

    [Fact]
    public void El_perfil_publico_solo_lleva_los_obtenidos()
    {
        var obtenidos = Logros.Obtenidos(Datos(truekes: 3, donaciones: 1), Ahora);
        Assert.Equal(new[] { "primer-intercambio", "truekero" }, obtenidos.Select(l => l.Clave));
        Assert.All(obtenidos, l => Assert.True(l.Obtenido));
    }
}
