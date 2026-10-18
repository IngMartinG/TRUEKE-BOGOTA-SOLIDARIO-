using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>Las funciones de administración exigen una sesión iniciada con verificación en dos pasos (claim amr = "mfa").</summary>
public sealed class DosFactoresRequirement : IAuthorizationRequirement
{
    public const string CodigoError = "2fa_requerido_admin";
}

public sealed class DosFactoresHandler : AuthorizationHandler<DosFactoresRequirement>
{
    private readonly SeguridadOpciones _o;
    public DosFactoresHandler(IOptions<SeguridadOpciones> o) => _o = o.Value;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, DosFactoresRequirement requirement)
    {
        if (!_o.ExigirDosFactoresModeradores || context.User.HasClaim("amr", "mfa")) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Si lo único que falla es el 2FA, el 403 lo dice claramente (codigo "2fa_requerido_admin") para que el front guíe al usuario.</summary>
public sealed class ResultadoAutorizacionHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _porDefecto = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        var fallidos = result.Forbidden ? result.AuthorizationFailure?.FailedRequirements.ToList() ?? [] : [];
        // Los requisitos de la cuenta van primero (mayoría de edad y luego la política y los términos vigentes): sin ellos
        // no sirve de nada activar el 2FA.
        var soloRequisitos = fallidos.Count > 0
            && fallidos.All(r => r is MayoriaEdadRequirement or LegalAlDiaRequirement or DosFactoresRequirement);
        if (soloRequisitos && fallidos.Any(r => r is MayoriaEdadRequirement) && context.User.HasClaim("edad", "menor"))
        {
            await Problemas.EscribirAsync(context, StatusCodes.Status403Forbidden, MenorDeEdadDetectadoException.Mensaje,
                codigo: MenorDeEdadDetectadoException.Codigo);
            return;
        }
        if (soloRequisitos && fallidos.Any(r => r is MayoriaEdadRequirement))
        {
            await Problemas.EscribirAsync(context, StatusCodes.Status403Forbidden,
                "Registra tu fecha de nacimiento para seguir: Trueke Bogotá Solidario es solo para personas mayores de 18 años.",
                codigo: MayoriaEdadRequirement.CodigoError);
            return;
        }
        if (soloRequisitos && fallidos.Any(r => r is LegalAlDiaRequirement))
        {
            await Problemas.EscribirAsync(context, StatusCodes.Status403Forbidden,
                "Actualizamos la política de tratamiento de datos y los términos de uso: léelos y acéptalos para seguir.",
                codigo: LegalAlDiaRequirement.CodigoError);
            return;
        }
        if (fallidos.Count > 0 && fallidos.All(r => r is DosFactoresRequirement))
        {
            await Problemas.EscribirAsync(context, StatusCodes.Status403Forbidden,
                "Activa la verificación en dos pasos y vuelve a iniciar sesión con tu código para usar las funciones de administración.",
                codigo: DosFactoresRequirement.CodigoError);
            return;
        }
        await _porDefecto.HandleAsync(next, context, policy, result);
    }
}
