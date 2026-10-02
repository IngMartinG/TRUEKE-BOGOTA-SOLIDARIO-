using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

public static class AutenticacionJwt
{
    public static IServiceCollection AddAutenticacionJwt(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOpciones>>((o, jwt) =>
            {
                var j = jwt.Value;
                o.MapInboundClaims = false; // los claims se leen con su nombre real: sub, role, sv
                o.RequireHttpsMetadata = true;
                o.SaveToken = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = j.Issuer,
                    ValidateAudience = true, ValidAudience = j.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(j.Key)),
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // bloquea "alg":"none" y cambios de algoritmo
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                o.Events = new JwtBearerEvents
                {
                    // WebSockets no permiten la cabecera Authorization: SOLO en la ruta de hubs se acepta el token por query.
                    // En cualquier otra ruta un ?access_token= se ignora (evita tokens en URLs, historiales y logs de proxies).
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")
                            && ctx.Request.Query.TryGetValue("access_token", out var token) && !string.IsNullOrEmpty(token))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    },
                    // Revocación: el token solo vale si su versión de seguridad coincide con la actual del usuario
                    // (cambió de rol/contraseña, cerró sesiones o fue eliminado → el token deja de servir).
                    OnTokenValidated = async ctx =>
                    {
                        var sub = ctx.Principal?.FindFirst("sub")?.Value;
                        var sv = ctx.Principal?.FindFirst("sv")?.Value;
                        if (!Guid.TryParse(sub, out var id) || !int.TryParse(sv, out var version))
                        {
                            ctx.Fail("Token sin identidad válida.");
                            return;
                        }
                        var sesiones = ctx.HttpContext.RequestServices.GetRequiredService<ISesionService>();
                        if (!await sesiones.EsVigenteAsync(id, version)) ctx.Fail("Sesión revocada.");
                    },
                    OnChallenge = async ctx =>
                    {
                        ctx.HandleResponse();
                        await Problemas.EscribirAsync(ctx.HttpContext, StatusCodes.Status401Unauthorized);
                    },
                    OnForbidden = ctx => Problemas.EscribirAsync(ctx.HttpContext, StatusCodes.Status403Forbidden)
                };
            });

        services.AddAuthorization(o =>
        {
            // Seguro por defecto: todo exige sesión salvo lo marcado [AllowAnonymous].
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            // Jerarquía SuperUsuario ⊃ Administrador ⊃ Cliente; además, sesión con verificación en dos pasos
            o.AddPolicy(Politicas.Moderador, p => p.RequireRole("Administrador", "SuperUsuario").AddRequirements(new DosFactoresRequirement()));
            o.AddPolicy(Politicas.SuperUsuario, p => p.RequireRole("SuperUsuario").AddRequirements(new DosFactoresRequirement()));
        });
        services.AddSingleton<IAuthorizationHandler, DosFactoresHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, ResultadoAutorizacionHandler>();
        return services;
    }
}
