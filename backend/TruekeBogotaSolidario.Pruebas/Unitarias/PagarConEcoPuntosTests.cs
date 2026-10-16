using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>1 Eco-Punto = 100 COP en Destacar y Verificar; la persona elige cuántos usa y nunca se canjean solos.</summary>
public class PagarConEcoPuntosTests
{
    private static async Task<(Guid Ana, Guid PubId)> PreparadoAsync(EntornoNegocio e, int puntos)
    {
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        await e.ModificarUsuarioAsync(ana, u => // saldo exacto (el registro ya trae los puntos de bienvenida)
        {
            var diferencia = puntos - u.SaldoEcoPuntos;
            if (diferencia > 0) u.AcreditarEcoPuntos(diferencia, ConceptoEcoPuntos.Recarga);
            else if (diferencia < 0) u.DebitarEcoPuntos(-diferencia, ConceptoEcoPuntos.CanjeDescuento);
        });
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(ana, new CrearPublicacionRequest
        {
            Titulo = "Bicicleta", Descripcion = "Buen estado", CategoriaId = 1, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Centro"
        }));
        return (ana, pub.Id);
    }

    private static IniciarPagoRequest Destacar(Guid pub, int puntos)
        => new() { Concepto = ConceptoPagoDto.Destacar, PublicacionId = pub, PuntosAUsar = puntos, FacturaANombre = false, AceptoConsumidorFinal = true };

    private static Task<PagoIniciadoDto> IniciarAsync(EntornoNegocio e, Guid actor, IniciarPagoRequest r)
        => e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(actor, r));

    private static async Task<int> SaldoAsync(EntornoNegocio e, Guid actor)
        => (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(actor))).Saldo;

    private static async Task<int> FacturasAsync(EntornoNegocio e)
    {
        using var scope = e.Proveedor.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Facturas.CountAsync();
    }

    [Fact]
    public async Task Sin_elegirlo_no_se_canjea_ningun_punto_aunque_haya_saldo()
    {
        using var e = new EntornoNegocio();
        var (ana, pub) = await PreparadoAsync(e, 500);

        var cot = await e.EnScopeAsync<IPagoService, CotizacionDto?>(s => s.CotizarAsync(ana, ConceptoPagoDto.Destacar));
        Assert.Equal(0, cot!.PuntosACanjear);
        Assert.Equal(PoliticaEcoPuntos.PrecioDestacarCop, cot.TotalCop);
        Assert.Equal(60, cot.PuntosParaCubrirTodo);
        Assert.Equal(60, cot.PuntosMaximos);

        var pago = await IniciarAsync(e, ana, Destacar(pub, 0));
        Assert.Equal(PoliticaEcoPuntos.PrecioDestacarCop, pago.MontoCop);
        Assert.Equal(500, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task Pago_parcial_vale_100_pesos_por_punto_y_si_falla_devuelve_los_puntos()
    {
        using var e = new EntornoNegocio();
        var (ana, pub) = await PreparadoAsync(e, 100);

        var pago = await IniciarAsync(e, ana, Destacar(pub, 30));
        Assert.Equal(3_000, pago.MontoCop);
        Assert.False(pago.AplicadoConPuntos);
        Assert.Equal(70, await SaldoAsync(e, ana));

        await e.EnScopeAsync<IPagoService, PagoEstadoDto>(s => s.SimularResultadoAsync(ana, pago.Referencia, aprobado: false));
        Assert.Equal(100, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task Cubierto_con_puntos_se_aplica_al_instante_sin_pasarela_ni_factura()
    {
        using var e = new EntornoNegocio();
        var (ana, pub) = await PreparadoAsync(e, 75);

        var pago = await IniciarAsync(e, ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Destacar, PublicacionId = pub, PuntosAUsar = 60 });
        Assert.True(pago.AplicadoConPuntos);
        Assert.Equal(0, pago.MontoCop);
        Assert.Equal(15, await SaldoAsync(e, ana));
        Assert.Equal(0, await FacturasAsync(e));
        var publicacion = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.ObtenerAsync(ana, pub));
        Assert.True(publicacion.Destacada);

        var historial = await e.EnScopeAsync<IEcoPuntosService, PaginaDto<MovimientoEcoPuntosDto>>(s => s.MovimientosAsync(ana, 1, 10));
        Assert.Contains(historial.Items, m => m.Puntos == -60 && m.Concepto == "CanjeDescuento");
    }

    [Fact]
    public async Task Con_plan_Empresa_el_descuento_se_aplica_antes_y_cubrir_todo_cuesta_menos_puntos()
    {
        using var e = new EntornoNegocio();
        var (ana, pub) = await PreparadoAsync(e, 100);
        await e.PagarAsync(ana, new IniciarPagoRequest { Concepto = ConceptoPagoDto.Empresa, FacturaANombre = false, AceptoConsumidorFinal = true });

        var cot = await e.EnScopeAsync<IPagoService, CotizacionDto?>(s => s.CotizarAsync(ana, ConceptoPagoDto.Destacar));
        Assert.Equal(4_800, cot!.PrecioConPlanCop);
        Assert.Equal(48, cot.PuntosParaCubrirTodo);

        var pago = await IniciarAsync(e, ana, Destacar(pub, 48));
        Assert.True(pago.AplicadoConPuntos);
        Assert.Equal(52, await SaldoAsync(e, ana));
    }

    [Theory]
    [InlineData(50, 100)]  // dejaría $1.000: menos del mínimo que cobra la pasarela
    [InlineData(61, 100)]  // más de lo que cuesta
    [InlineData(40, 20)]   // más de los que tiene
    public async Task Combinaciones_imposibles_se_rechazan_sin_tocar_el_saldo(int puntos, int saldo)
    {
        using var e = new EntornoNegocio();
        var (ana, pub) = await PreparadoAsync(e, saldo);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => IniciarAsync(e, ana, Destacar(pub, puntos)));
        Assert.Equal(saldo, await SaldoAsync(e, ana));
    }

    [Fact]
    public async Task Verificar_se_puede_cubrir_con_200_puntos_y_queda_en_revision()
    {
        using var e = new EntornoNegocio();
        var (ana, _) = await PreparadoAsync(e, 200);
        var pago = await IniciarAsync(e, ana, new IniciarPagoRequest
        {
            Concepto = ConceptoPagoDto.Verificar, DocumentoUrl = "https://docs.test/cedula.pdf", PuntosAUsar = 200
        });
        Assert.True(pago.AplicadoConPuntos);
        Assert.Equal(0, await SaldoAsync(e, ana));
        var perfil = await e.EnScopeAsync<IAuthService, UsuarioDto>(s => s.ObtenerPerfilAsync(ana));
        Assert.Equal("Pendiente", perfil.EstadoVerificacion);
    }

    [Fact]
    public async Task Los_planes_no_aceptan_puntos()
    {
        using var e = new EntornoNegocio();
        var (ana, _) = await PreparadoAsync(e, 500);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => IniciarAsync(e, ana, new IniciarPagoRequest
        {
            Concepto = ConceptoPagoDto.Premium, PuntosAUsar = 100, FacturaANombre = false, AceptoConsumidorFinal = true
        }));
        Assert.Equal(500, await SaldoAsync(e, ana));
    }
}
