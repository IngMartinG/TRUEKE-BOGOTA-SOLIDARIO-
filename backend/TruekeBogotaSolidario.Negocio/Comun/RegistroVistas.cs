using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Repositorios;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Cuenta vistas de publicaciones sin tocar la base de datos en cada visita: se acumulan en memoria y un proceso en
/// segundo plano las suma cada minuto. Una misma persona (o IP, guardada como hash) cuenta una vez cada pocas horas.
/// </summary>
public interface IRegistroVistas
{
    void Registrar(Guid publicacionId, string visitante);
    IReadOnlyList<(Guid PublicacionId, DateTime Dia, int Vistas)> Extraer();
    /// <summary>Si el volcado falló, las vistas vuelven a la cola para el siguiente intento.</summary>
    void Devolver(IEnumerable<(Guid PublicacionId, DateTime Dia, int Vistas)> vistas);
}

public sealed class RegistroVistasEnMemoria : IRegistroVistas, IDisposable
{
    private readonly MemoryCache _vistos = new(new MemoryCacheOptions { SizeLimit = 200_000 }); // memoria acotada
    private readonly ConcurrentDictionary<(Guid, DateTime), int> _pendientes = new();
    private readonly TimeProvider _reloj;

    public RegistroVistasEnMemoria(TimeProvider reloj) => _reloj = reloj;

    /// <summary>Identificador anónimo de una IP: nunca se guarda la IP en claro.</summary>
    public static string VisitantePorIp(string ip)
        => "ip:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("trueke-vistas:" + ip)))[..16];

    public void Registrar(Guid publicacionId, string visitante)
    {
        if (string.IsNullOrWhiteSpace(visitante)) return;
        var clave = $"{publicacionId:N}|{visitante}";
        if (_vistos.TryGetValue(clave, out _)) return;
        _vistos.Set(clave, true, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = Limites.VentanaVistaUnica });
        _pendientes.AddOrUpdate((publicacionId, _reloj.GetUtcNow().UtcDateTime.Date), 1, (_, n) => n + 1);
    }

    public IReadOnlyList<(Guid PublicacionId, DateTime Dia, int Vistas)> Extraer()
    {
        var lista = new List<(Guid, DateTime, int)>();
        foreach (var clave in _pendientes.Keys)
            if (_pendientes.TryRemove(clave, out var n) && n > 0) lista.Add((clave.Item1, clave.Item2, n));
        return lista;
    }

    public void Devolver(IEnumerable<(Guid PublicacionId, DateTime Dia, int Vistas)> vistas)
    {
        foreach (var (pub, dia, n) in vistas)
            _pendientes.AddOrUpdate((pub, dia), n, (_, actual) => actual + n);
    }

    public void Dispose() => _vistos.Dispose();
}

/// <summary>Suma a la base de datos las vistas acumuladas (cada minuto y al apagar la API).</summary>
public sealed class VolcadoVistasHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);
    private readonly IRegistroVistas _registro;
    private readonly IServiceScopeFactory _fabrica;
    private readonly ILogger<VolcadoVistasHostedService> _log;

    public VolcadoVistasHostedService(IRegistroVistas registro, IServiceScopeFactory fabrica, ILogger<VolcadoVistasHostedService> log)
    {
        _registro = registro; _fabrica = fabrica; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(Intervalo, ct); }
            catch (OperationCanceledException) { break; }
            await VolcarAsync();
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        await base.StopAsync(ct);
        await VolcarAsync(); // no perder las vistas del último minuto al reiniciar
    }

    internal async Task VolcarAsync()
    {
        var lote = _registro.Extraer();
        if (lote.Count == 0) return;
        try
        {
            using var scope = _fabrica.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IEstadisticaRepository>().SumarVistasAsync(lote);
        }
        catch (Exception ex)
        {
            _registro.Devolver(lote);
            _log.LogWarning(ex, "No se pudieron guardar {Total} registros de vistas; se reintentará", lote.Count);
        }
    }
}
