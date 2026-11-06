using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Presentacion.TiempoReal;

/// <summary>
/// Tras cualquier acción de moderación que salió bien (aprobar, rechazar, resolver, responder…), las insignias del panel
/// de todos los moderadores se actualizan en vivo: así nadie ve como pendiente lo que un colega ya resolvió. Un solo
/// filtro evita repetir el aviso en cada servicio.
/// </summary>
public sealed class PendientesModeracionFiltro : IAsyncActionFilter
{
    private const string PrefijoAdmin = "/api/v1/admin/";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ejecutada = await next();
        var http = context.HttpContext;
        if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)) return;
        if (!http.Request.Path.StartsWithSegments(PrefijoAdmin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return;
        if (ejecutada.Exception is not null || ejecutada.Result is IStatusCodeActionResult { StatusCode: >= 400 }) return;
        await http.RequestServices.GetRequiredService<IAvisosModeracion>().ActualizarContadoresAsync(); // nunca falla
    }
}
