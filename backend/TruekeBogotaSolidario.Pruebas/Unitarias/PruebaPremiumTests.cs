using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Premium gratis 7 días: una vez por cuenta, sin cobro al terminar, y pagar durante la prueba no pierde nada.</summary>
public class PruebaPremiumTests
{
    private static Task<EcoPuntosResumenDto> ActivarAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ActivarPruebaPremiumAsync(actor));

    private static Task<EcoPuntosResumenDto> ResumenAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(actor));

    private static Task PlanAsync(EntornoNegocio e, Guid actor, ConceptoPagoDto plan)
        => e.PagarAsync(actor, new IniciarPagoRequest { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = plan });

    [Fact]
    public async Task Da_Premium_por_7_dias_con_1_destacado_gratis_y_luego_vuelve_a_Individual_sin_cobrar()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        Assert.True((await ResumenAsync(e, ana)).PruebaPremiumDisponible);

        var activo = await ActivarAsync(e, ana);
        Assert.Equal("Premium", activo.TipoCuenta);
        Assert.True(activo.EnPruebaPremium);
        Assert.False(activo.PruebaPremiumDisponible);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisPruebaPremium, activo.DestacadosGratisRestantes);
        Assert.Equal(PoliticaEcoPuntos.MaxPublicacionesPremium, activo.MaxPublicacionesActivas);
        Assert.Equal(PoliticaEcoPuntos.DescuentoPremiumPorcentaje, activo.DescuentoPlanPorcentaje);
        Assert.Equal(reloj.GetUtcNow().UtcDateTime.AddDays(PoliticaEcoPuntos.DiasPruebaPremium), activo.PlanVigenteHasta);

        reloj.Avanzar(TimeSpan.FromDays(PoliticaEcoPuntos.DiasPruebaPremium).Add(TimeSpan.FromMinutes(1)));
        var vencido = await ResumenAsync(e, ana);
        Assert.Equal("Individual", vencido.TipoCuenta);
        Assert.False(vencido.EnPruebaPremium);
        Assert.Equal(0, vencido.DestacadosGratisRestantes);
    }

    [Fact]
    public async Task Solo_se_puede_usar_una_vez()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await ActivarAsync(e, ana);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ActivarAsync(e, ana));
        reloj.Avanzar(TimeSpan.FromDays(30));
        var otraVez = await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ActivarAsync(e, ana));
        Assert.Contains("Ya usaste", otraVez.Message);
    }

    [Fact]
    public async Task No_es_para_quien_ya_pago_un_plan()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await PlanAsync(e, ana, ConceptoPagoDto.Premium);
        Assert.False((await ResumenAsync(e, ana)).PruebaPremiumDisponible);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ActivarAsync(e, ana));
    }

    [Fact]
    public async Task Exige_la_cuenta_completa()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.ModificarUsuarioAsync(ana, u => u.QuitarFoto());
        await Assert.ThrowsAsync<FotoPerfilRequeridaException>(() => ActivarAsync(e, ana));
    }

    [Fact]
    public async Task Pagar_Premium_durante_la_prueba_suma_los_dias_y_da_el_cupo_completo()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await ActivarAsync(e, ana);
        reloj.Avanzar(TimeSpan.FromDays(2));

        await PlanAsync(e, ana, ConceptoPagoDto.Premium);
        var pagado = await ResumenAsync(e, ana);
        Assert.False(pagado.EnPruebaPremium);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisPremium, pagado.DestacadosGratisRestantes);
        // los 5 días que le quedaban de prueba + 30 pagados
        Assert.Equal(reloj.GetUtcNow().UtcDateTime.AddDays(5 + PoliticaEcoPuntos.DuracionSuscripcionDias), pagado.PlanVigenteHasta);
    }

    [Fact]
    public async Task Durante_la_prueba_se_puede_pasar_a_Empresa()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await ActivarAsync(e, ana);
        await PlanAsync(e, ana, ConceptoPagoDto.Empresa);
        var resumen = await ResumenAsync(e, ana);
        Assert.Equal("Empresa", resumen.TipoCuenta);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisEmpresa, resumen.DestacadosGratisRestantes);
    }
}
