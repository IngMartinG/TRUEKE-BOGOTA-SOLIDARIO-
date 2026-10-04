using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Servicios;

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
            try { await CerrarSolicitudesVencidasAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { _log.LogError(ex, "Fallo cerrando solicitudes vencidas"); }
            try { await RecordarVencimientosAsync(); }
            catch (Exception ex) { _log.LogError(ex, "Fallo enviando recordatorios de vencimiento de planes"); }
            try
            {
                using var scope = _fabrica.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IFacturacionService>().EmitirSimuladasAsync(200);
            }
            catch (Exception ex) { _log.LogError(ex, "Fallo en la facturación simulada"); }

            try { await Task.Delay(Intervalo, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Aceptadas con una confirmación hace más de 7 días → Completadas; sin confirmaciones hace más de 30 → No concretadas.</summary>
    internal async Task<int> CerrarSolicitudesVencidasAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> ids;
        using (var scope = _fabrica.CreateScope())
            ids = await scope.ServiceProvider.GetRequiredService<ISolicitudService>().ListarParaCierreAutomaticoAsync(200);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var scope = _fabrica.CreateScope(); // un scope por solicitud: un fallo no contamina al resto
                await scope.ServiceProvider.GetRequiredService<ISolicitudService>().CerrarAutomaticamenteAsync(id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "No se pudo cerrar la solicitud {SolicitudId}; se reintentará", id);
            }
        }
        if (ids.Count > 0) _log.LogInformation("Cierre automático: {Total} solicitudes procesadas", ids.Count);
        return ids.Count;
    }

    /// <summary>
    /// Avisa (una sola vez por vencimiento) a quien tiene un plan Premium o Empresa que vence en los próximos días.
    /// Sin cobro automático, este aviso es lo que evita que el plan se pierda por olvido.
    /// </summary>
    internal async Task<int> RecordarVencimientosAsync()
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        using var scope = _fabrica.CreateScope();
        var sp = scope.ServiceProvider;
        var usuarios = await sp.GetRequiredService<IUsuarioRepository>()
            .ListarPlanesPorVencerAsync(ahora, ahora.AddDays(PoliticaEcoPuntos.DiasRecordatorioVencimiento), 200);
        if (usuarios.Count == 0) return 0;
        foreach (var u in usuarios) u.MarcarRecordatorioVencimiento(u.FechaVencimientoSuscripcion!.Value);
        await sp.GetRequiredService<IUnidadDeTrabajo>().GuardarCambiosAsync(); // primero se marca: nunca se envía dos veces

        var notificador = sp.GetRequiredService<INotificador>();
        var correo = sp.GetRequiredService<ICorreoSaliente>();
        var frontend = sp.GetRequiredService<IOptions<UrlsOpciones>>().Value.Frontend.TrimEnd('/');
        foreach (var u in usuarios)
        {
            var plan = u.TipoCuenta == TipoCuenta.Empresa ? "Empresa" : "Premium";
            var vence = u.FechaVencimientoSuscripcion!.Value;
            await notificador.NotificarAsync(u.Id, TiposNotificacion.PlanPorVencer,
                $"Tu plan {plan} vence el {vence:yyyy-MM-dd}. Renuévalo para no perder tus beneficios.");
            if (!u.AvisosCorreoPlanes) continue; // apagado en Cuenta → Avisos (la notificación en la app sí llega)
            correo.Encolar(PlantillaCorreo.Crear(u.Correo, $"Tu plan {plan} vence pronto",
                $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
                new[]
                {
                    $"Tu plan {plan} de Trueke vence el {vence:yyyy-MM-dd} (UTC).",
                    "Si lo renuevas antes, los días se suman al final del periodo actual: no pierdes ni un día.",
                    "Al vencer, tus publicaciones siguen visibles, pero pierdes los destacados gratis, el descuento y las estadísticas detalladas."
                },
                ("Renovar mi plan", $"{frontend}/eco-puntos")));
        }
        _log.LogInformation("Recordatorios de vencimiento enviados: {Total}", usuarios.Count);
        return usuarios.Count;
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
