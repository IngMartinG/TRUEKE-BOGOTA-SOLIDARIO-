using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Plan anual (12 meses por el precio de 10) y recargas con bono: precio, duración, cupo, bono y reembolsos.</summary>
public class PlanAnualYBonosTests
{
    private static IniciarPagoRequest Plan(ConceptoPagoDto plan)
        => new() { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = plan };

    private static IniciarPagoRequest Recarga(int montoCop)
        => new() { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = montoCop };

    private static Task<EcoPuntosResumenDto> ResumenAsync(EntornoNegocio e, Guid actor)
        => e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(actor));

    private static async Task<Guid> SuperAsync(EntornoNegocio e)
    {
        var super = (await e.RegistrarAsync("super")).Usuario.Id;
        await e.HacerModeradorAsync(super);
        return super;
    }

    // ------------------------------------------------------------------ Plan anual
    [Theory]
    [InlineData(ConceptoPagoDto.PremiumAnual, "Premium", PoliticaEcoPuntos.PrecioPremiumAnualCop)]
    [InlineData(ConceptoPagoDto.EmpresaAnual, "Empresa", PoliticaEcoPuntos.PrecioEmpresaAnualCop)]
    public async Task El_plan_anual_cobra_su_precio_y_dura_365_dias(ConceptoPagoDto concepto, string plan, int precio)
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;

        var pago = await e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(ana, Plan(concepto)));
        Assert.Equal(precio, pago.MontoCop);
        await e.EnScopeAsync<IPagoService, PagoEstadoDto>(s => s.SimularResultadoAsync(ana, pago.Referencia, aprobado: true));

        var resumen = await ResumenAsync(e, ana);
        Assert.Equal(plan, resumen.TipoCuenta);
        Assert.Equal(reloj.GetUtcNow().UtcDateTime.AddDays(PoliticaEcoPuntos.DuracionSuscripcionAnualDias), resumen.PlanVigenteHasta);
        var factura = Assert.Single(await e.EnScopeAsync<IFacturacionService, IReadOnlyList<FacturaDto>>(s => s.ListarMiasAsync(ana)));
        Assert.Equal(precio, factura.TotalCop);
        Assert.Contains("anual", factura.Descripcion);
    }

    [Fact]
    public void El_anual_es_12_meses_por_el_precio_de_10()
    {
        Assert.Equal(10 * PoliticaEcoPuntos.PrecioPremiumCop, PoliticaEcoPuntos.PrecioPremiumAnualCop);
        Assert.Equal(10 * PoliticaEcoPuntos.PrecioEmpresaCop, PoliticaEcoPuntos.PrecioEmpresaAnualCop);
    }

    [Fact]
    public async Task Pasar_de_mensual_a_anual_suma_los_dias_sin_perder_el_cupo()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, Plan(ConceptoPagoDto.Premium));
        var mensual = await ResumenAsync(e, ana);

        await e.PagarAsync(ana, Plan(ConceptoPagoDto.PremiumAnual));
        var anual = await ResumenAsync(e, ana);
        Assert.Equal(mensual.PlanVigenteHasta!.Value.AddDays(PoliticaEcoPuntos.DuracionSuscripcionAnualDias), anual.PlanVigenteHasta);
        Assert.Equal(mensual.ProximaRecargaDestacadosUtc, anual.ProximaRecargaDestacadosUtc);
    }

    [Fact]
    public async Task Con_el_plan_anual_el_cupo_de_destacados_se_repone_cada_30_dias()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var tienda = (await e.RegistrarAsync("tienda")).Usuario.Id;
        await e.PagarAsync(tienda, Plan(ConceptoPagoDto.EmpresaAnual));
        await e.ModificarUsuarioAsync(tienda, u => u.UsarDestacadoGratis(reloj.GetUtcNow().UtcDateTime));
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisEmpresa - 1, (await ResumenAsync(e, tienda)).DestacadosGratisRestantes);

        reloj.Avanzar(TimeSpan.FromDays(31));
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisEmpresa, (await ResumenAsync(e, tienda)).DestacadosGratisRestantes);

        reloj.Avanzar(TimeSpan.FromDays(170)); // día 201: sigue vigente y con el cupo completo
        var resumen = await ResumenAsync(e, tienda);
        Assert.Equal("Empresa", resumen.TipoCuenta);
        Assert.Equal(PoliticaEcoPuntos.DestacadosGratisEmpresa, resumen.DestacadosGratisRestantes);
    }

    [Fact]
    public async Task No_se_dejan_abiertos_el_pago_mensual_y_el_anual_del_mismo_plan()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(ana, Plan(ConceptoPagoDto.Premium)));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(ana, Plan(ConceptoPagoDto.PremiumAnual))));
    }

    [Fact]
    public async Task Con_Premium_vigente_no_se_compra_Empresa_anual()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, Plan(ConceptoPagoDto.PremiumAnual));
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(ana, Plan(ConceptoPagoDto.EmpresaAnual))));
    }

    [Fact]
    public async Task Reembolsar_el_anual_descuenta_los_365_dias()
    {
        var reloj = new RelojFalso();
        using var e = new EntornoNegocio(reloj: reloj);
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, Plan(ConceptoPagoDto.Premium));
        var mensual = (await ResumenAsync(e, ana)).PlanVigenteHasta;
        var anual = await e.PagarAsync(ana, Plan(ConceptoPagoDto.PremiumAnual));

        var super = await SuperAsync(e);
        await e.EnScopeAsync<IAdministracionService, PagoAdminDto>(s => s.MarcarReembolsadoAsync(super, anual, "Retracto del anual"));
        var resumen = await ResumenAsync(e, ana);
        Assert.Equal("Premium", resumen.TipoCuenta); // queda el mes que pagó aparte
        Assert.Equal(mensual, resumen.PlanVigenteHasta);
    }

    // ------------------------------------------------------------------ Recargas con bono
    [Theory]
    [InlineData(10_000, 100, 10)]
    [InlineData(20_000, 200, 30)]
    [InlineData(50_000, 500, 100)]
    [InlineData(15_000, 150, 0)] // monto libre: sin bono
    public async Task Los_paquetes_dan_bono_y_el_monto_libre_no(int monto, int puntos, int bono)
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.PagarAsync(ana, Recarga(monto));

        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida + puntos + bono, (await ResumenAsync(e, ana)).Saldo);
        var historial = await e.EnScopeAsync<IEcoPuntosService, PaginaDto<MovimientoEcoPuntosDto>>(s => s.MovimientosAsync(ana, 1, 20));
        Assert.Equal(puntos, Assert.Single(historial.Items, m => m.Concepto == "Recarga").Puntos);
        if (bono > 0) Assert.Equal(bono, Assert.Single(historial.Items, m => m.Concepto == "BonoRecarga").Puntos);
        else Assert.DoesNotContain(historial.Items, m => m.Concepto == "BonoRecarga");
    }

    [Theory]
    [InlineData(4_900)]
    [InlineData(2_000)]
    public async Task La_recarga_minima_es_de_5000(int monto)
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() =>
            e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(ana, Recarga(monto))));
    }

    [Fact]
    public async Task Reembolsar_una_recarga_con_bono_retira_la_recarga_y_el_bono()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var referencia = await e.PagarAsync(ana, Recarga(20_000)); // +200 +30

        var super = await SuperAsync(e);
        await e.EnScopeAsync<IAdministracionService, PagoAdminDto>(s => s.MarcarReembolsadoAsync(super, referencia, "Reversión"));
        Assert.Equal(PoliticaEcoPuntos.PuntosBienvenida, (await ResumenAsync(e, ana)).Saldo);
    }

    [Fact]
    public async Task Si_ya_gasto_parte_el_reembolso_retira_solo_lo_que_queda()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        var referencia = await e.PagarAsync(ana, Recarga(10_000)); // saldo 10 + 100 + 10 = 120
        await e.ModificarUsuarioAsync(ana, u => u.RetirarEcoPuntosHasta(100)); // gasta 100: le quedan 20

        var super = await SuperAsync(e);
        await e.EnScopeAsync<IAdministracionService, PagoAdminDto>(s => s.MarcarReembolsadoAsync(super, referencia, "Reversión"));
        Assert.Equal(0, (await ResumenAsync(e, ana)).Saldo);
    }

    [Fact]
    public async Task La_politica_publica_trae_los_paquetes_y_el_plan_anual()
    {
        using var e = new EntornoNegocio();
        var p = await e.EnScopeAsync<IEcoPuntosService, PoliticaEcoPuntosDto>(s => Task.FromResult(s.ObtenerPolitica()));
        Assert.Equal(PoliticaEcoPuntos.PrecioPremiumAnualCop, p.PrecioPremiumAnualCop);
        Assert.Equal(PoliticaEcoPuntos.DuracionSuscripcionAnualDias, p.DuracionSuscripcionAnualDias);
        Assert.Equal(5_000, p.RecargaMinimaCop);
        Assert.Contains(new PaqueteRecargaDto(20_000, 200, 30), p.PaquetesRecarga!);
    }
}
