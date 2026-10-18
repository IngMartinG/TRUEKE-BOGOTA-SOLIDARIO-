using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Repositorios;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Cuenta las PQRS públicas radicadas desde el último resumen. El canal público no exige cuenta: avisar a cada moderador
/// por cada radicación permitiría inundar su bandeja, su correo y sus dispositivos. Las PQRS de usuarios con cuenta
/// siguen avisando una por una.
/// </summary>
public sealed class ResumenPqrPublicas
{
    private int _pendientes;

    public void Registrar() => Interlocked.Increment(ref _pendientes);

    /// <summary>Devuelve las pendientes y deja el contador en cero.</summary>
    public int Extraer() => Interlocked.Exchange(ref _pendientes, 0);

    /// <summary>Si el aviso falló, las pendientes vuelven para el siguiente resumen.</summary>
    public void Devolver(int cantidad) => Interlocked.Add(ref _pendientes, cantidad);

    public static string Mensaje(int cantidad) => cantidad == 1
        ? "Nueva PQRS pública: revísala en PQRS."
        : $"{cantidad} PQRS públicas nuevas: revísalas en PQRS.";
}

/// <summary>
/// Cada <see cref="Intervalo"/>, un solo aviso por moderador con las PQRS públicas que llegaron en esa ventana.
/// El contador vive en la memoria de cada instancia: con varias instancias, cada una envía su propio resumen. Si la
/// instancia se reinicia antes de enviarlo, las PQRS siguen en la cola de moderación (solo se pierde el aviso).
/// </summary>
public sealed class ResumenPqrPublicasHostedService : BackgroundService
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(10);
    private readonly ResumenPqrPublicas _resumen;
    private readonly IServiceScopeFactory _fabrica;
    private readonly ILogger<ResumenPqrPublicasHostedService> _log;

    public ResumenPqrPublicasHostedService(ResumenPqrPublicas resumen, IServiceScopeFactory fabrica, ILogger<ResumenPqrPublicasHostedService> log)
    {
        _resumen = resumen; _fabrica = fabrica; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(Intervalo, ct); }
            catch (OperationCanceledException) { break; }
            await EnviarAsync();
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        await EnviarAsync(); // no perder el resumen de la última ventana al reiniciar
    }

    /// <returns>Cuántas PQRS públicas incluyó el resumen (0 si no había).</returns>
    internal async Task<int> EnviarAsync()
    {
        var cantidad = _resumen.Extraer();
        if (cantidad == 0) return 0;
        try
        {
            using var scope = _fabrica.CreateScope();
            var sp = scope.ServiceProvider;
            var notificador = sp.GetRequiredService<INotificador>();
            foreach (var moderador in await sp.GetRequiredService<IUsuarioRepository>().ListarIdsModeradoresAsync())
                await notificador.NotificarAsync(moderador, TiposNotificacion.PqrNueva, ResumenPqrPublicas.Mensaje(cantidad));
            return cantidad;
        }
        catch (Exception ex)
        {
            _resumen.Devolver(cantidad);
            _log.LogWarning(ex, "No se pudo avisar a la moderación de {Cantidad} PQRS públicas; se reintentará", cantidad);
            return 0;
        }
    }
}
