using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>
/// Revisa con el detector de rostros las fotos de perfil anteriores a la regla (estado "sin revisar"): las que no muestran un
/// rostro quedan rechazadas con plazo para cambiarlas, y la persona recibe un aviso y un correo. Por lotes pequeños con
/// pausas (el servidor gratis tiene poca CPU). Es idempotente: varias instancias no se estorban.
/// </summary>
public sealed class RevisionFotosPerfilHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);
    private static readonly TimeSpan PausaEntreLotes = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _fabrica;
    private readonly IAlmacenArchivos _almacen;
    private readonly FotosPerfilOpciones _opciones;
    private readonly ILogger<RevisionFotosPerfilHostedService> _log;

    public RevisionFotosPerfilHostedService(IServiceScopeFactory fabrica, IAlmacenArchivos almacen, IOptions<FotosPerfilOpciones> opciones,
        ILogger<RevisionFotosPerfilHostedService> log)
    {
        _fabrica = fabrica; _almacen = almacen; _opciones = opciones.Value; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_almacen.Habilitado || !_opciones.DetectorHabilitado || !_opciones.RevisarExistentes) return;
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(3), ct); // no competir con el arranque
            using var temporizador = new PeriodicTimer(Intervalo);
            do await RevisarPendientesAsync(ct);
            while (await temporizador.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    private async Task RevisarPendientesAsync(CancellationToken ct)
    {
        var total = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                using var scope = _fabrica.CreateScope();
                var revisadas = await scope.ServiceProvider.GetRequiredService<IFotoPerfilService>().RevisarFotosAnterioresAsync(_opciones.TamanoLote, ct);
                total += revisadas;
                if (revisadas < _opciones.TamanoLote) break;
                await Task.Delay(PausaEntreLotes, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { _log.LogError(ex, "Fallo revisando las fotos de perfil anteriores; se reintentará más tarde"); }
        if (total > 0) _log.LogInformation("Fotos de perfil revisadas en segundo plano: {Total}", total);
    }
}
