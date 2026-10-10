using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>
/// Una vez por arranque, crea las miniaturas que falten (fotos publicadas antes de que existieran o cuya miniatura falló).
/// Las fotos nuevas ya nacen con miniatura, así que normalmente no hay nada que hacer y solo cuesta listar el contenedor.
/// </summary>
public sealed class MiniaturasHostedService : BackgroundService
{
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
            await _almacen.GenerarMiniaturasFaltantesAsync(ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _log.LogError(ex, "Fallo creando las miniaturas faltantes; se reintentará en el próximo arranque"); }
    }
}
