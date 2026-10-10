using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace TruekeBogotaSolidario.Presentacion.Monitoreo;

/// <summary>
/// Azure Application Insights vía OpenTelemetry, solo si hay cadena de conexión (APPLICATIONINSIGHTS_CONNECTION_STRING).
/// Envía peticiones, dependencias (SQL sin texto de consultas, HTTP), excepciones y logs. Diseñado para el plan gratis
/// (5 GB/mes): sin /health, logs del framework solo desde Warning y muestreo configurable.
/// Nunca se registran cuerpos de peticiones, contraseñas, tokens ni la query string.
/// </summary>
public static class Telemetria
{
    public const string NombreServicio = "trueke-api";

    public static bool Configurar(WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var conexion = config["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? config["Monitoreo:ConnectionString"];
        if (string.IsNullOrWhiteSpace(conexion)) return false;

        // 1.0 = todo (lo normal con poco tráfico). Si el consumo se acerca a los 5 GB gratis, bajar (p. ej. 0.25).
        var muestreo = Math.Clamp(config.GetValue("Monitoreo:Muestreo", 1.0f), 0.01f, 1.0f);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(NombreServicio))
            // Se registra ANTES del exportador para que limpie cada actividad antes de enviarla.
            .WithTracing(t => t.AddProcessor(new QuitarDatosSensibles()))
            .UseAzureMonitor(o =>
            {
                o.ConnectionString = conexion;
                o.SamplingRatio = muestreo;
            });

        // Los chequeos de salud (Render, un monitor externo) no son tráfico real: solo gastarían cuota.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(o =>
            o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"));

        // El framework es muy conversador en Information; lo de la aplicación sí se envía desde Information.
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>("System", LogLevel.Warning);
        return true;
    }
}

/// <summary>
/// Quita la query string de las URLs antes de exportar. El hub de SignalR recibe el JWT en ?access_token= y algunas
/// llamadas a terceros llevan llaves en la URL; aunque la instrumentación ya redacta valores, aquí no sale ninguno.
/// </summary>
public sealed class QuitarDatosSensibles : BaseProcessor<Activity>
{
    private static readonly string[] EtiquetasUrl = ["url.full", "http.url", "http.target"];

    public override void OnEnd(Activity actividad)
    {
        actividad.SetTag("url.query", null);
        foreach (var etiqueta in EtiquetasUrl)
        {
            if (actividad.GetTagItem(etiqueta) is string url && url.IndexOf('?') is var i and >= 0)
                actividad.SetTag(etiqueta, url[..i]);
        }
    }
}
