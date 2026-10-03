using System.Security.Claims;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

public static class ClaimsExtensions
{
    /// <summary>Usuario actuante: SIEMPRE sale del token (claim sub), nunca del cuerpo ni de la URL.</summary>
    public static Guid IdActual(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException();

    public static Guid? IdActualOpcional(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;

    /// <summary>
    /// Identificador anónimo de quien mira una publicación (para contar vistas únicas): el usuario si inició sesión o
    /// un hash de su IP. Los bots conocidos no cuentan. Nunca se guarda la IP en claro.
    /// </summary>
    public static string? VisitanteParaEstadisticas(this HttpContext ctx)
    {
        var agente = ctx.Request.Headers.UserAgent.ToString();
        if (agente.Length == 0 || agente.Contains("bot", StringComparison.OrdinalIgnoreCase)
            || agente.Contains("crawler", StringComparison.OrdinalIgnoreCase) || agente.Contains("spider", StringComparison.OrdinalIgnoreCase))
            return null;
        if (ctx.User.IdActualOpcional() is { } id) return "u:" + id.ToString("N");
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        return ip is null ? null : Negocio.Comun.RegistroVistasEnMemoria.VisitantePorIp(ip);
    }
}

public static class Politicas
{
    public const string Moderador = "Moderador";
    public const string SuperUsuario = "SuperUsuario";
    public const string LimiteAuth = "auth";
    public const string LimiteWebhook = "webhook";
    public const string LimiteEscritura = "escritura";
    public const string LimiteRefresco = "refresco";
}
