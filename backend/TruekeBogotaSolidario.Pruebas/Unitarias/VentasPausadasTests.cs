using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>
/// Lanzamiento sin cobro (Pagos:VentasHabilitadas=false): no se inicia ningún pago con dinero, pero lo que cubren los
/// Eco-Puntos sigue funcionando y los pagos ya iniciados terminan su curso.
/// </summary>
public class VentasPausadasTests
{
    private static EntornoNegocio SinVentas(string? donacionUrl = null) => new(extra: new Dictionary<string, string?>
    {
        ["Pagos:VentasHabilitadas"] = "false",
        ["Donaciones:Url"] = donacionUrl,
    });

    private static async Task<(Guid Ana, Guid PubId)> PreparadoAsync(EntornoNegocio e, int puntosExtra = 0)
    {
        var ana = (await e.RegistrarAsync("ana")).Usuario.Id;
        if (puntosExtra > 0) await e.ModificarUsuarioAsync(ana, u => u.AcreditarEcoPuntos(puntosExtra, ConceptoEcoPuntos.Recarga));
        var pub = await e.EnScopeAsync<IPublicacionService, PublicacionDto>(s => s.CrearAsync(ana, new CrearPublicacionRequest
        {
            Titulo = "Bicicleta", Descripcion = "Buen estado", CategoriaId = 1, Modo = ModoDto.Trueke, Condicion = CondicionDto.Usado, Localidad = "Centro"
        }));
        return (ana, pub.Id);
    }

    private static IniciarPagoRequest Pedido(ConceptoPagoDto concepto, Guid? pub = null, int puntos = 0, int? recarga = null) => new()
    {
        Concepto = concepto, PublicacionId = pub, PuntosAUsar = puntos, MontoRecargaCop = recarga,
        FacturaANombre = false, AceptoConsumidorFinal = true
    };

    private static Task<PagoIniciadoDto> IniciarAsync(EntornoNegocio e, Guid actor, IniciarPagoRequest r)
        => e.EnScopeAsync<IPagoService, PagoIniciadoDto>(s => s.IniciarAsync(actor, r));

    [Theory]
    [InlineData(ConceptoPagoDto.Recarga)]
    [InlineData(ConceptoPagoDto.Premium)]
    [InlineData(ConceptoPagoDto.EmpresaAnual)]
    public async Task Recargas_y_planes_quedan_pausados(ConceptoPagoDto concepto)
    {
        using var e = SinVentas();
        var (ana, _) = await PreparadoAsync(e);

        await Assert.ThrowsAsync<VentasPausadasException>(() =>
            IniciarAsync(e, ana, Pedido(concepto, recarga: concepto == ConceptoPagoDto.Recarga ? 10_000 : null)));
    }

    [Fact]
    public async Task Destacar_con_dinero_queda_pausado_y_no_reserva_puntos()
    {
        using var e = SinVentas();
        var (ana, pub) = await PreparadoAsync(e);
        var saldo = (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana))).Saldo;

        await Assert.ThrowsAsync<VentasPausadasException>(() => IniciarAsync(e, ana, Pedido(ConceptoPagoDto.Destacar, pub, puntos: 10)));
        var resumen = await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana));
        Assert.Equal(saldo, resumen.Saldo);
    }

    [Fact]
    public async Task Destacar_cubierto_con_eco_puntos_sigue_funcionando()
    {
        using var e = SinVentas();
        var (ana, pub) = await PreparadoAsync(e, puntosExtra: 500);
        var cot = await e.EnScopeAsync<IPagoService, CotizacionDto?>(s => s.CotizarAsync(ana, ConceptoPagoDto.Destacar));

        var pago = await IniciarAsync(e, ana, Pedido(ConceptoPagoDto.Destacar, pub, puntos: cot!.PuntosParaCubrirTodo));

        Assert.True(pago.AplicadoConPuntos);
        Assert.Equal(0, pago.MontoCop);
    }

    [Fact]
    public async Task Un_pago_iniciado_antes_de_pausar_se_aprueba_igual()
    {
        using var e = SinVentas();
        var (ana, _) = await PreparadoAsync(e);
        var antes = (await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana))).Saldo;
        var referencia = "TRK-" + Guid.NewGuid().ToString("N");
        using (var scope = e.Proveedor.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            db.Pagos.Add(new Pago(ana, ConceptoPago.Recarga, 10_000, referencia, 0, null, null, DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        var estado = await e.EnScopeAsync<IPagoService, PagoEstadoDto>(s => s.SimularResultadoAsync(ana, referencia, aprobado: true));

        Assert.Equal("Aprobado", estado.Estado.ToString());
        var resumen = await e.EnScopeAsync<IEcoPuntosService, EcoPuntosResumenDto>(s => s.ResumenAsync(ana));
        Assert.Equal(antes + 10_000 / PoliticaEcoPuntos.CopPorEcoPunto + PoliticaEcoPuntos.BonoRecarga(10_000), resumen.Saldo);
    }

    [Fact]
    public async Task La_configuracion_publica_avisa_al_front()
    {
        using (var e = SinVentas("https://pagos.ejemplo.co/aporte"))
        {
            var c = await e.EnScopeAsync<IConfiguracionService, ConfiguracionPublicaDto>(s => Task.FromResult(s.ObtenerPublica()));
            Assert.False(c.VentasHabilitadas);
            Assert.Equal("https://pagos.ejemplo.co/aporte", c.DonacionUrl);
        }
        using (var e = new EntornoNegocio())
        {
            var c = await e.EnScopeAsync<IConfiguracionService, ConfiguracionPublicaDto>(s => Task.FromResult(s.ObtenerPublica()));
            Assert.True(c.VentasHabilitadas);
            Assert.Null(c.DonacionUrl);
        }
    }

    [Fact]
    public void Una_url_de_aportes_sin_https_no_deja_arrancar()
    {
        Assert.ThrowsAny<Exception>(() =>
        {
            using var e = SinVentas("http://pagos.ejemplo.co/aporte");
            using var scope = e.Proveedor.CreateScope();
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DonacionesOpciones>>().Value.ToString();
        });
    }
}
