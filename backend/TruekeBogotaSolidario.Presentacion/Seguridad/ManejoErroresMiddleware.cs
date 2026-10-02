using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>
/// Respuesta de error uniforme (ProblemDetails). Las excepciones de negocio conocidas exponen su mensaje; cualquier otra
/// devuelve un mensaje genérico y el detalle (stack trace) va SOLO al log. Nunca se filtra información interna al navegador.
/// </summary>
public sealed class ManejoErroresMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly ILogger<ManejoErroresMiddleware> _log;

    public ManejoErroresMiddleware(RequestDelegate siguiente, ILogger<ManejoErroresMiddleware> log)
    {
        _siguiente = siguiente;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _siguiente(ctx);
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            // el cliente cerró la conexión: no es un error del servidor
        }
        catch (BadHttpRequestException ex)
        {
            await Responder(ctx, ex.StatusCode, ex.StatusCode == 413 ? null : "Solicitud mal formada.");
        }
        catch (UnauthorizedAccessException)
        {
            await Responder(ctx, 401, "Sesión no válida.");
        }
        catch (Exception ex)
        {
            var e = TraductorErrores.Traducir(ex);
            if (e.EsInesperado) _log.LogError(ex, "Error no controlado en {Metodo} {Ruta}", ctx.Request.Method, ctx.Request.Path.Value);
            else _log.LogInformation("Solicitud rechazada ({Estado}): {Tipo}", e.Estado, ex.GetType().Name);
            await Responder(ctx, e.Estado, e.Titulo);
        }
    }

    private static Task Responder(HttpContext ctx, int estado, string? titulo)
    {
        if (ctx.Response.HasStarted) return Task.CompletedTask;
        ctx.Response.Clear();
        return Problemas.EscribirAsync(ctx, estado, titulo);
    }
}
