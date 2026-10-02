using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>
/// Formato ÚNICO de error para el front (RFC 9457 / ProblemDetails, "application/problem+json"):
/// { type, title, status, traceId, errors? }. "title" es el mensaje mostrable; "errors" solo en validaciones (400).
/// </summary>
public static class Problemas
{
    public const string TipoContenido = "application/problem+json";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string TipoPara(int estado) => estado switch
    {
        400 => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        401 => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
        403 => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        404 => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
        405 => "https://tools.ietf.org/html/rfc9110#section-15.5.6",
        409 => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
        413 => "https://tools.ietf.org/html/rfc9110#section-15.5.14",
        415 => "https://tools.ietf.org/html/rfc9110#section-15.5.16",
        429 => "https://tools.ietf.org/html/rfc6585#section-4",
        503 => "https://tools.ietf.org/html/rfc9110#section-15.6.4",
        _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
    };

    public static string TituloPorDefecto(int estado) => estado switch
    {
        400 => "Solicitud no válida.",
        401 => "Autenticación requerida o sesión expirada.",
        403 => "No tienes permiso para realizar esta acción.",
        404 => "Recurso no encontrado.",
        405 => "Método no permitido.",
        409 => "El recurso fue modificado por otra operación. Vuelve a intentarlo.",
        413 => "La solicitud es demasiado grande.",
        415 => "Tipo de contenido no soportado. Usa application/json.",
        429 => "Demasiadas solicitudes. Intenta de nuevo en un momento.",
        503 => "Servicio no disponible temporalmente.",
        _ => "Error interno del servidor."
    };

    public static ProblemDetails Crear(HttpContext ctx, int estado, string? titulo = null)
    {
        var p = new ProblemDetails { Type = TipoPara(estado), Title = titulo ?? TituloPorDefecto(estado), Status = estado };
        p.Extensions["traceId"] = ctx.TraceIdentifier;
        return p;
    }

    /// <param name="codigo">Identificador estable opcional (p. ej. "2fa_requerido") para que el front reaccione sin leer el texto.</param>
    public static async Task EscribirAsync(HttpContext ctx, int estado, string? titulo = null, CancellationToken ct = default, string? codigo = null)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.StatusCode = estado;
        ctx.Response.ContentType = TipoContenido;
        var cuerpo = new Dictionary<string, object?>
        {
            ["type"] = TipoPara(estado),
            ["title"] = titulo ?? TituloPorDefecto(estado),
            ["status"] = estado,
            ["traceId"] = ctx.TraceIdentifier
        };
        if (codigo is not null) cuerpo["codigo"] = codigo;
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(cuerpo, Json), ct);
    }

    /// <summary>400 de validación: claves en camelCase (como las propiedades JSON que envía Angular).</summary>
    public static IActionResult Validacion(ActionContext ctx)
    {
        var errores = ctx.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => ACamelCase(e.Key),
                e => e.Value!.Errors.Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "Valor no válido." : x.ErrorMessage).ToArray());
        var p = new ValidationProblemDetails(errores)
        {
            Type = TipoPara(400),
            Title = "Hay datos no válidos en la solicitud.",
            Status = 400
        };
        p.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        return new BadRequestObjectResult(p) { ContentTypes = { TipoContenido } };
    }

    /// <summary>"Lat" → "lat", "Datos.Titulo" → "datos.titulo", "$.modo" se conserva.</summary>
    private static string ACamelCase(string clave)
        => string.Join('.', clave.Split('.').Select(s => s.Length > 0 && char.IsUpper(s[0]) ? char.ToLowerInvariant(s[0]) + s[1..] : s));
}
