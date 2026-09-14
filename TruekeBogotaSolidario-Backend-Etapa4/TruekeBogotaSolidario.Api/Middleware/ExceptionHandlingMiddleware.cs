using System.Net;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Api.Common.Exceptions;

namespace TruekeBogotaSolidario.Api.Middleware;

// ==========================================================================
// MIDDLEWARE GLOBAL (transversal a todas las capas)
// Intercepta cualquier excepcion no controlada que suba desde
// Controller -> Service -> Repository y la traduce a una respuesta
// estandarizada ProblemDetails (RFC 7807), tal como exige el requerimiento
// de la Etapa 4 / Punto 5 del taller de referencia.
// ==========================================================================
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, "Recurso no encontrado"),
            BusinessRuleException => (HttpStatusCode.BadRequest, "Regla de negocio violada"),
            ArgumentException => (HttpStatusCode.BadRequest, "Solicitud invalida"),
            _ => (HttpStatusCode.InternalServerError, "Error interno del servidor")
        };

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Detail = exception.Message,
            Type = $"https://httpstatuses.com/{(int)statusCode}",
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;

        return context.Response.WriteAsJsonAsync(problemDetails);
    }
}
