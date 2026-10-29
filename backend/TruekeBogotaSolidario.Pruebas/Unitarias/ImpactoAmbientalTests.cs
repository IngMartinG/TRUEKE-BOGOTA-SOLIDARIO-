using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Metodología de impacto 2.0.0: kilos declarados o típicos de la categoría × factor del material.</summary>
public class ImpactoAmbientalTests
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Categoria Hogar = new(4, "Hogar y muebles", "Muebles");

    private static Publicacion Pub(decimal? kg = null, MaterialObjeto? material = null)
        => new(Guid.NewGuid(), new DatosPublicacion("Mesa", "De madera", Hogar, ModoTransaccion.Trueke, CondicionProducto.Usado,
            null, "11001", "Suba", null, null, null, null, PesoEstimadoKg: kg, Material: material));

    private static Dictionary<MaterialObjeto, FactorImpacto> Factores() => new()
    {
        [MaterialObjeto.Madera] = new(MaterialObjeto.Madera, 1.2m, "Prueba", "2.0.0", Ahora),
        [MaterialObjeto.Mixto] = new(MaterialObjeto.Mixto, 1.5m, "Prueba", "2.0.0", Ahora),
        [MaterialObjeto.Textil] = new(MaterialObjeto.Textil, 8m, "Prueba", "2.0.0", Ahora),
    };

    [Fact]
    public void El_peso_declarado_y_su_material_mandan()
        => Assert.Equal((12m, OrigenPeso.Declarado, MaterialObjeto.Madera), ImpactoAmbiental.PesoYMaterial(Pub(12, MaterialObjeto.Madera)));

    [Fact]
    public void Sin_peso_usa_el_tipico_de_la_categoria_y_lo_marca_como_estimado()
    {
        Assert.Equal((8m, OrigenPeso.EstimadoPorCategoria, MaterialObjeto.Mixto), ImpactoAmbiental.PesoYMaterial(Pub()));
        // Con material pero sin peso: el peso es el típico; el material, el declarado.
        Assert.Equal((8m, OrigenPeso.EstimadoPorCategoria, MaterialObjeto.Madera), ImpactoAmbiental.PesoYMaterial(Pub(material: MaterialObjeto.Madera)));
        // Una categoría desconocida usa la de "Otros".
        Assert.Equal(ImpactoAmbiental.PorCategoria[7], ImpactoAmbiental.Tipico(99));
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(501)]
    public void El_peso_declarado_tiene_limites(double kg)
        => Assert.Throws<ReglaDeNegocioException>(() => Pub((decimal)kg));

    [Fact]
    public void Un_trueke_con_objetos_registra_cada_objeto_en_su_sentido_con_el_factor_y_la_version()
    {
        var pedida = Pub(10, MaterialObjeto.Madera);
        var ofrecida = Pub();
        var duenio = Guid.NewGuid();
        var solicitante = Guid.NewGuid();
        var t = new Transaccion(pedida.Id, Guid.NewGuid(), duenio, solicitante, ModoTransaccion.Trueke, true, true, Ahora);

        var r = ImpactoAmbiental.Registros(t, pedida, [ofrecida], Factores()).ToList();

        Assert.Equal(2, r.Count);
        Assert.Equal((duenio, solicitante, 10m, 12m, "2.0.0"), (r[0].OferenteId, r[0].ReceptorId, r[0].Kg, r[0].KgCo2, r[0].Version));
        Assert.Equal((solicitante, duenio, 8m, 12m), (r[1].OferenteId, r[1].ReceptorId, r[1].Kg, r[1].KgCo2)); // 8 kg × 1,5 (mixto)
        Assert.Equal(OrigenPeso.EstimadoPorCategoria, r[1].OrigenPeso);
    }

    [Fact]
    public void Cambiar_un_factor_exige_fuente_y_version_nueva_y_no_toca_lo_registrado()
    {
        var factores = Factores();
        var factor = factores[MaterialObjeto.Madera];
        var pub = Pub(10, MaterialObjeto.Madera);
        var t = new Transaccion(pub.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ModoTransaccion.Donacion, true, true, Ahora);
        var registro = ImpactoAmbiental.Registros(t, pub, [], factores).Single();

        Assert.Throws<ReglaDeNegocioException>(() => factor.Actualizar(2m, "Estudio nuevo", "2.0.0", null, Ahora));
        Assert.Throws<ReglaDeNegocioException>(() => factor.Actualizar(2m, "", "2.1.0", null, Ahora));
        Assert.Throws<ReglaDeNegocioException>(() => factor.Actualizar(2m, "Estudio nuevo", "dos", null, Ahora));
        factor.Actualizar(2m, "Estudio nuevo", "2.1.0", null, Ahora);

        Assert.Equal(2m, factor.KgCo2PorKg);
        Assert.Equal((12m, 1.2m, "2.0.0"), (registro.KgCo2, registro.FactorKgCo2PorKg, registro.Version));
    }

    [Fact]
    public void La_version_vigente_compara_numeros_y_no_texto()
    {
        var f = Factores().Values.ToList();
        f[0].Actualizar(1m, "Fuente", "2.10.0", null, Ahora);
        f[1].Actualizar(1m, "Fuente", "2.9.0", null, Ahora);
        Assert.Equal("2.10.0", ImpactoService.VersionVigente(f));
    }

    [Theory]
    [InlineData(PeriodoImpactoDto.Mes, 2026, 10)]
    [InlineData(PeriodoImpactoDto.Trimestre, 2026, 10)]
    [InlineData(PeriodoImpactoDto.Anio, 2026, 1)]
    public void Los_periodos_empiezan_a_medianoche_de_Colombia(PeriodoImpactoDto periodo, int anio, int mes)
    {
        Assert.Equal(new DateTime(anio, mes, 1, 5, 0, 0, DateTimeKind.Utc), ImpactoService.InicioPeriodoUtc(periodo, Ahora));
        Assert.Null(ImpactoService.InicioPeriodoUtc(PeriodoImpactoDto.Todo, Ahora));
        // El 1 de noviembre a las 2 a. m. UTC todavía es 31 de octubre en Bogotá.
        Assert.Equal(new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc),
            ImpactoService.InicioPeriodoUtc(PeriodoImpactoDto.Mes, new DateTime(2026, 11, 1, 2, 0, 0, DateTimeKind.Utc)));
    }
}
