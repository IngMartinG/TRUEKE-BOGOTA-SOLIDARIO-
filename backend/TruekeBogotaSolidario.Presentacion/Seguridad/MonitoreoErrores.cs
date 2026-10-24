using System.Text.RegularExpressions;
using Sentry;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>
/// Monitoreo de errores con Sentry (plan gratis), opcional: solo se activa si hay <c>Sentry:Dsn</c> (variable
/// <c>Sentry__Dsn</c>). Reporta lo mismo que ya va al log como error (los 500 de <see cref="ManejoErroresMiddleware"/> y
/// los fallos de los procesos en segundo plano), nunca los 4xx de dominio.
/// Ley 1581: no viaja ningún dato personal. Sin cuerpo, cabeceras, cookies ni query (el hub lleva ahí el token), sin
/// usuario ni IP, y los correos que aparezcan en un mensaje se reemplazan. Sin trazas de rendimiento (cupo gratis).
/// </summary>
public static partial class MonitoreoErrores
{
    public static void UsarMonitoreoErrores(this WebApplicationBuilder builder)
    {
        var dsn = builder.Configuration["Sentry:Dsn"];
        if (string.IsNullOrWhiteSpace(dsn)) return;

        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = dsn;
            o.Environment = builder.Environment.EnvironmentName.ToLowerInvariant();
            // Render define RENDER_GIT_COMMIT en cada despliegue: cada error queda asociado a su versión.
            o.Release = Environment.GetEnvironmentVariable("RENDER_GIT_COMMIT") is { Length: > 0 } commit ? $"trueke-api@{commit}" : null;
            o.SendDefaultPii = false;
            o.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
            o.MinimumEventLevel = LogLevel.Error;
            // Lo informativo puede nombrar datos de la persona: solo advertencias y errores como contexto del error.
            o.MinimumBreadcrumbLevel = LogLevel.Warning;
            o.TracesSampleRate = 0;
            o.AutoSessionTracking = false;
            o.SetBeforeSend((evento, _) => Limpiar(evento));
            o.SetBeforeBreadcrumb((miga, _) => Limpiar(miga));
        });
    }

    /// <summary>Quita del evento todo lo que pueda identificar a una persona o dar acceso (tokens en la URL).</summary>
    internal static SentryEvent Limpiar(SentryEvent evento)
    {
        var solicitud = evento.Request;
        solicitud.Url = SinQuery(solicitud.Url);
        solicitud.QueryString = null;
        solicitud.Cookies = null;
        solicitud.Data = null;
        solicitud.Headers.Clear();
        solicitud.Env.Clear();
        evento.User = new SentryUser();
        evento.ServerName = null;

        if (evento.Message is { } mensaje)
        {
            mensaje.Message = LimpiarTexto(mensaje.Message);
            mensaje.Formatted = LimpiarTexto(mensaje.Formatted);
        }
        foreach (var excepcion in evento.SentryExceptions ?? [])
            excepcion.Value = LimpiarTexto(excepcion.Value);
        return evento;
    }

    /// <summary>
    /// Las migas de llamadas HTTP salientes llevan la URL: las firmadas de Supabase tienen el token en la query.
    /// Se llama al agregar la miga, así que la copia (con la hora actual) conserva su orden.
    /// </summary>
    internal static Breadcrumb Limpiar(Breadcrumb miga)
    {
        var datos = miga.Data?.ToDictionary(d => d.Key, d => d.Key == "url" ? SinQuery(d.Value) ?? "" : LimpiarTexto(d.Value) ?? "");
        var mensaje = LimpiarTexto(miga.Message);
        var igual = mensaje == miga.Message && (datos is null || datos.All(d => miga.Data![d.Key] == d.Value));
        return igual ? miga : new Breadcrumb(mensaje!, miga.Type!, datos, miga.Category, miga.Level);
    }

    internal static string? SinQuery(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;
        var corte = url.IndexOfAny(['?', '#']);
        return corte < 0 ? url : url[..corte];
    }

    /// <summary>Correos y query de las URL citadas en un mensaje de error.</summary>
    internal static string? LimpiarTexto(string? texto) =>
        string.IsNullOrEmpty(texto) ? texto : Correo().Replace(QueryEnUrl().Replace(texto, "$1"), "[correo]");

    [GeneratedRegex(@"[^\s@<>""'(),;:]+@[^\s@<>""'(),;:]+\.[a-zA-Z]{2,}")]
    private static partial Regex Correo();

    [GeneratedRegex(@"(\bhttps?://[^\s?#""'<>]*)[?#][^\s""'<>]*")]
    private static partial Regex QueryEnUrl();
}
