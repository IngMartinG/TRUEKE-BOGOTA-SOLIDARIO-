using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Repositorios;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Limpieza periódica (minimización de datos): tokens de refresco y enlaces de un solo uso vencidos, y notificaciones
/// leídas hace más de 90 días. Por lotes, para no bloquear la base de datos. Es idempotente: varias instancias no se estorban.
/// </summary>
public sealed class MantenimientoHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);
    private const int Lote = 500;

    private readonly IServiceScopeFactory _fabrica;
    private readonly TimeProvider _reloj;
    private readonly ILogger<MantenimientoHostedService> _log;

    public MantenimientoHostedService(IServiceScopeFactory fabrica, TimeProvider reloj, ILogger<MantenimientoHostedService> log)
    {
        _fabrica = fabrica; _reloj = reloj; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), ct); } // no competir con el arranque
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try { await LimpiarAsync(); }
            catch (Exception ex) { _log.LogError(ex, "Fallo en el mantenimiento periódico"); }

            try { await Task.Delay(Intervalo, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    internal async Task<int> LimpiarAsync()
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        using var scope = _fabrica.CreateScope();
        var sp = scope.ServiceProvider;
        var total = await sp.GetRequiredService<ISesionRefreshRepository>().PurgarAsync(ahora.AddDays(-1), Lote);
        total += await sp.GetRequiredService<ITokenUsoUnicoRepository>().PurgarAsync(ahora.AddDays(-1), Lote);
        total += await sp.GetRequiredService<INotificacionRepository>().PurgarLeidasAsync(ahora.AddDays(-90), Lote);
        if (total > 0) _log.LogInformation("Mantenimiento: {Total} registros vencidos eliminados", total);
        return total;
    }
}
