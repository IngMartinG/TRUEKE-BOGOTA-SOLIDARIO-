using System.Security.Claims;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

public static class ClaimsExtensions
{
    /// <summary>Usuario actuante: SIEMPRE sale del token (claim sub), nunca del cuerpo ni de la URL.</summary>
    public static Guid IdActual(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : throw new UnauthorizedAccessException();

    public static Guid? IdActualOpcional(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
}

public static class Politicas
{
    public const string Moderador = "Moderador";
    public const string SuperUsuario = "SuperUsuario";
    public const string LimiteAuth = "auth";
    public const string LimiteWebhook = "webhook";
    public const string LimiteEscritura = "escritura";
}
