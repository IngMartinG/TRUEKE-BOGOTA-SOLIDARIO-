using Microsoft.AspNetCore.Authorization;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>
/// La plataforma es solo para mayores de 18 años: toda acción con sesión exige el claim "edad" = "ok" (la persona registró su
/// fecha de nacimiento). Las cuentas anteriores a la regla solo pueden lo marcado con <see cref="PermitirSinFechaNacimientoAttribute"/>.
/// </summary>
public sealed class MayoriaEdadRequirement : IAuthorizationRequirement
{
    public const string CodigoError = "fecha_nacimiento_requerida";
}

/// <summary>
/// Lo que una cuenta sin fecha de nacimiento, o sin aceptar las versiones vigentes de la política y los términos, sí puede
/// hacer: completarlo, gestionar su sesión y su seguridad, ver sus avisos, radicar PQR y ejercer el habeas data (exportar o
/// eliminar sus datos nunca se bloquea).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PermitirSinFechaNacimientoAttribute : Attribute;

public sealed class MayoriaEdadHandler : AuthorizationHandler<MayoriaEdadRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MayoriaEdadRequirement requirement)
    {
        var endpoint = context.Resource switch
        {
            HttpContext http => http.GetEndpoint(),
            Endpoint e => e,
            _ => null
        };
        if (context.User.HasClaim("edad", "ok") || endpoint?.Metadata.GetMetadata<PermitirSinFechaNacimientoAttribute>() is not null)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Cuando cambia la versión de la política de datos o de los términos, toda acción con sesión exige el claim "legal" = "ok"
/// (aceptó las vigentes). Exentos: los mismos endpoints de <see cref="PermitirSinFechaNacimientoAttribute"/>.
/// </summary>
public sealed class LegalAlDiaRequirement : IAuthorizationRequirement
{
    public const string CodigoError = "terminos_desactualizados";
}

public sealed class LegalAlDiaHandler : AuthorizationHandler<LegalAlDiaRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, LegalAlDiaRequirement requirement)
    {
        var endpoint = context.Resource switch
        {
            HttpContext http => http.GetEndpoint(),
            Endpoint e => e,
            _ => null
        };
        if (context.User.HasClaim("legal", "ok") || endpoint?.Metadata.GetMetadata<PermitirSinFechaNacimientoAttribute>() is not null)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
