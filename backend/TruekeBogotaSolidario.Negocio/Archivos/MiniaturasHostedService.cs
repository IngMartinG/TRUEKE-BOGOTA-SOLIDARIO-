using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>
/// Mantenimiento del almacenamiento:
/// - una vez por arranque, crea las miniaturas que falten (fotos publicadas antes de que existieran o cuya miniatura falló);
/// - cada pocas horas, borra las subidas que nadie usó (el contenedor de subidas no es un hosting gratuito).
/// </summary>
public sealed class MiniaturasHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    private readonly IAlmacenArchivos _almacen;
    private readonly ILogger<MiniaturasHostedService> _log;

    public MiniaturasHostedService(IAlmacenArchivos almacen, ILogger<MiniaturasHostedService> log)
    {
        _almacen = almacen; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_almacen.Habilitado) return;
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct); // no competir con el arranque
            await IntentarAsync(() => _almacen.GenerarMiniaturasFaltantesAsync(ct), "creando las miniaturas faltantes");
            using var temporizador = new PeriodicTimer(Intervalo);
            do await IntentarAsync(() => _almacen.EliminarSubidasVencidasAsync(ct), "borrando las subidas sin usar");
            while (await temporizador.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    private async Task IntentarAsync(Func<Task<int>> trabajo, string que)
    {
        try { await trabajo(); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log.LogError(ex, "Fallo {Que}; se reintentará más tarde", que); }
    }
}
