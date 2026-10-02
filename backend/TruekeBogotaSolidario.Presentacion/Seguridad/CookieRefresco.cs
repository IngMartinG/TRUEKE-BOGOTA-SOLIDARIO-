using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>
/// El token de refresco viaja SOLO en esta cookie: HttpOnly (JavaScript no la lee, un XSS no puede robarla), Secure,
/// SameSite configurable (Strict por defecto) y limitada a /api/v1/auth (no acompaña al resto de peticiones).
/// El prefijo __Secure- obliga al navegador a aceptarla únicamente por HTTPS.
/// </summary>
public static class CookieRefresco
{
    public const string Nombre = "__Secure-trueke_rt";
    public const string Ruta = "/api/v1/auth";

    private static SameSiteMode SameSite(HttpContext ctx)
    {
        var valor = ctx.RequestServices.GetRequiredService<IConfiguration>()["Auth:CookieSameSite"];
        return Enum.TryParse<SameSiteMode>(valor, ignoreCase: true, out var modo) && modo != SameSiteMode.Unspecified ? modo : SameSiteMode.Strict;
    }

    public static void Escribir(HttpContext ctx, ResultadoAutenticacion r)
        => ctx.Response.Cookies.Append(Nombre, r.TokenRefresco, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSite(ctx),
            Path = Ruta,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(r.TokenRefrescoExpiraUtc, DateTimeKind.Utc)),
            IsEssential = true
        });

    public static void Borrar(HttpContext ctx)
        => ctx.Response.Cookies.Delete(Nombre, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSite(ctx), Path = Ruta });

    public static string? Leer(HttpContext ctx) => ctx.Request.Cookies[Nombre];
}

/// <summary>
/// Anti-CSRF para los endpoints que se autentican con la cookie (refrescar, salir). Además de SameSite:
/// 1) exige la cabecera X-Trueke-Csrf (un formulario de otro sitio no puede enviarla y, por CORS, un fetch ajeno
///    necesita un preflight que solo aprueban los orígenes configurados); 2) si llega Origin, debe ser uno permitido.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ProteccionCsrfAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string Cabecera = "X-Trueke-Csrf";

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var req = context.HttpContext.Request;
        var cabeceraOk = req.Headers.TryGetValue(Cabecera, out var v) && v == "1";
        var origen = req.Headers.Origin.ToString();
        var permitidos = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()
            .GetSection("Cors:Origenes").Get<string[]>() ?? Array.Empty<string>();
        var mismoOrigen = $"{req.Scheme}://{req.Host}";
        var origenOk = string.IsNullOrEmpty(origen)
                       || string.Equals(origen, mismoOrigen, StringComparison.OrdinalIgnoreCase)
                       || permitidos.Contains(origen, StringComparer.OrdinalIgnoreCase);

        if (cabeceraOk && origenOk) return;
        context.Result = new EmptyResult();
        await Problemas.EscribirAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Solicitud no permitida (protección CSRF).");
    }
}
