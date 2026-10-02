using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio.Pagos;

/// <summary>
/// Cada pocos minutos revisa los pagos pendientes antiguos: si la pasarela ya los resolvió (webhook perdido) los aplica,
/// y si siguen sin resolverse los expira devolviendo los Eco-Puntos reservados. Un scope por pago: un fallo no contamina al resto.
/// </summary>
public sealed class ReconciliadorPagosHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _fabrica;
    private readonly PagosOpciones _opciones;
    private readonly ILogger<ReconciliadorPagosHostedService> _log;

    public ReconciliadorPagosHostedService(IServiceScopeFactory fabrica, IOptions<PagosOpciones> opciones, ILogger<ReconciliadorPagosHostedService> log)
    {
        _fabrica = fabrica; _opciones = opciones.Value; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var espera = TimeSpan.FromSeconds(_opciones.SegundosEntreReconciliaciones);
        while (!ct.IsCancellationRequested)
        {
            try { await CicloAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _log.LogError(ex, "Fallo en el ciclo de reconciliación de pagos"); }

            try { await Task.Delay(espera, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task CicloAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> ids;
        using (var scope = _fabrica.CreateScope())
            ids = await scope.ServiceProvider.GetRequiredService<IPagoService>().ListarPagosParaReconciliarAsync(100);

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var scope = _fabrica.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPagoService>().ReconciliarAsync(id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "No se pudo reconciliar el pago {PagoId}; se reintentará", id);
            }
        }
    }
}
