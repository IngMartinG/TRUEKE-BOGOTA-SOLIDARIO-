namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>Cabeceras de endurecimiento para una API JSON (sin contenido activo, sin iframes, sin caché de respuestas privadas).</summary>
public sealed class CabecerasSeguridadMiddleware
{
    private readonly RequestDelegate _siguiente;
    public CabecerasSeguridadMiddleware(RequestDelegate siguiente) => _siguiente = siguiente;

    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            var esSwagger = ctx.Request.Path.StartsWithSegments("/swagger");
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            h["Cross-Origin-Resource-Policy"] = "same-site";
            if (!esSwagger)
            {
                h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
                // Solo los datos públicos de referencia (p. ej. el catálogo de municipios) se marcan "public" a propósito;
                // todo lo demás nunca se guarda en cachés.
                if (!h.CacheControl.ToString().StartsWith("public", StringComparison.Ordinal) || ctx.Response.StatusCode != StatusCodes.Status200OK)
                    h["Cache-Control"] = "no-store";
            }
            return Task.CompletedTask;
        });
        return _siguiente(ctx);
    }
}
