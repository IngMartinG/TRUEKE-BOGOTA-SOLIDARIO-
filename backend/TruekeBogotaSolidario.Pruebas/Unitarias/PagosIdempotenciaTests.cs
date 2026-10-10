using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Pagos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class PagosIdempotenciaTests
{
    /// <summary>Pasarela falsa: responde con la transacción que la prueba configure (la "verdad" de Wompi).</summary>
    private sealed class PasarelaFalsa : IProveedorPagos
    {
        public TransaccionProveedor? Transaccion { get; set; }
        public int Consultas { get; private set; }
        public string Nombre => "Wompi";
        public string? LlavePublica => "pub_test_x";
        public string? FirmaIntegridad(string referencia, long montoCentavos, string moneda) => "firma";
        public Task<TransaccionProveedor?> ConsultarPorIdAsync(string transaccionId, CancellationToken ct)
        {
            Consultas++;
            return Task.FromResult(Transaccion?.Id == transaccionId ? Transaccion : null);
        }
        public Task<TransaccionProveedor?> ConsultarPorReferenciaAsync(string referencia, CancellationToken ct)
            => Task.FromResult(Transaccion?.Referencia == referencia ? Transaccion : null);
    }

    private static async Task<(EntornoNegocio Entorno, PasarelaFalsa Pasarela, Guid UsuarioId, PagoIniciadoDto Pago)> PrepararRecargaAsync(int montoCop)
    {
        var pasarela = new PasarelaFalsa();
        var entorno = new EntornoNegocio(pasarela);
        var sesion = await entorno.RegistrarAsync("pagador");
        var pago = await entorno.EnScopeAsync<IPagoService, PagoIniciadoDto>(p => p.IniciarAsync(sesion.Usuario.Id,
            new IniciarPagoRequest { FacturaANombre = false, AceptoConsumidorFinal = true, Concepto = ConceptoPagoDto.Recarga, MontoRecargaCop = montoCop }));
        return (entorno, pasarela, sesion.Usuario.Id, pago);
    }

    private static Task<int> SaldoAsync(EntornoNegocio e, Guid usuarioId)
        => e.EnScopeAsync<IEcoPuntosService, int>(async s => (await s.ResumenAsync(usuarioId)).Saldo);

    [Fact]
    public async Task Webhook_repetido_acredita_una_sola_vez()
    {
        var (entorno, pasarela, usuarioId, pago) = await PrepararRecargaAsync(10_000);
        using var _ = entorno;
        var saldoInicial = await SaldoAsync(entorno, usuarioId);

        pasarela.Transaccion = new TransaccionProveedor("tx-1", pago.Referencia, pago.MontoEnCentavos, "COP", "APPROVED");
        var evento = WebhookWompiTests.EventoFirmado(EntornoNegocio.SecretoEventos, "tx-1", "APPROVED", pago.MontoEnCentavos, pago.Referencia);

        for (var i = 0; i < 3; i++)
            await entorno.EnScopeAsync<IPagoService>(p => p.ProcesarEventoWompiAsync(evento));

        Assert.Equal(saldoInicial + 10_000 / 100, await SaldoAsync(entorno, usuarioId));
        var estado = await entorno.EnScopeAsync<IPagoService, PagoEstadoDto>(p => p.ObtenerEstadoAsync(usuarioId, pago.Referencia));
        Assert.Equal("Aprobado", estado.Estado);
    }

    [Fact]
    public async Task Webhook_con_firma_invalida_no_toca_nada()
    {
        var (entorno, pasarela, usuarioId, pago) = await PrepararRecargaAsync(10_000);
        using var _ = entorno;
        var saldoInicial = await SaldoAsync(entorno, usuarioId);
        pasarela.Transaccion = new TransaccionProveedor("tx-2", pago.Referencia, pago.MontoEnCentavos, "COP", "APPROVED");
        var falso = WebhookWompiTests.EventoFirmado("secreto-del-atacante", "tx-2", "APPROVED", pago.MontoEnCentavos, pago.Referencia);

        await Assert.ThrowsAsync<AutenticacionException>(() => entorno.EnScopeAsync<IPagoService>(p => p.ProcesarEventoWompiAsync(falso)));
        Assert.Equal(0, pasarela.Consultas);
        Assert.Equal(saldoInicial, await SaldoAsync(entorno, usuarioId));
    }

    [Fact]
    public async Task Monto_distinto_en_la_pasarela_no_acredita()
    {
        var (entorno, pasarela, usuarioId, pago) = await PrepararRecargaAsync(10_000);
        using var _ = entorno;
        var saldoInicial = await SaldoAsync(entorno, usuarioId);
        // el atacante pagó 100 COP reutilizando la referencia de una recarga de 10.000
        pasarela.Transaccion = new TransaccionProveedor("tx-3", pago.Referencia, 10_000, "COP", "APPROVED");
        var evento = WebhookWompiTests.EventoFirmado(EntornoNegocio.SecretoEventos, "tx-3", "APPROVED", 10_000, pago.Referencia);

        await entorno.EnScopeAsync<IPagoService>(p => p.ProcesarEventoWompiAsync(evento));
        Assert.Equal(saldoInicial, await SaldoAsync(entorno, usuarioId));
    }

    [Fact]
    public async Task Pago_rechazado_y_luego_aprobado_no_acredita()
    {
        var (entorno, pasarela, usuarioId, pago) = await PrepararRecargaAsync(5_000);
        using var _ = entorno;
        var saldoInicial = await SaldoAsync(entorno, usuarioId);

        pasarela.Transaccion = new TransaccionProveedor("tx-4", pago.Referencia, pago.MontoEnCentavos, "COP", "DECLINED");
        await entorno.EnScopeAsync<IPagoService>(p => p.ProcesarEventoWompiAsync(
            WebhookWompiTests.EventoFirmado(EntornoNegocio.SecretoEventos, "tx-4", "DECLINED", pago.MontoEnCentavos, pago.Referencia)));

        pasarela.Transaccion = pasarela.Transaccion with { Estado = "APPROVED" };
        await entorno.EnScopeAsync<IPagoService>(p => p.ProcesarEventoWompiAsync(
            WebhookWompiTests.EventoFirmado(EntornoNegocio.SecretoEventos, "tx-4", "APPROVED", pago.MontoEnCentavos, pago.Referencia)));

        Assert.Equal(saldoInicial, await SaldoAsync(entorno, usuarioId));
    }

    [Fact]
    public async Task Simulacion_no_disponible_con_pasarela_real()
    {
        var (entorno, _, usuarioId, pago) = await PrepararRecargaAsync(5_000);
        using var _e = entorno;
        await Assert.ThrowsAsync<AccesoDenegadoException>(() =>
            entorno.EnScopeAsync<IPagoService>(p => p.SimularResultadoAsync(usuarioId, pago.Referencia, true)));
    }
}
