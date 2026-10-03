using System.Globalization;
using Microsoft.AspNetCore.RateLimiting;
using StackExchange.Redis;

namespace TruekeBogotaSolidario.Presentacion.Seguridad;

/// <summary>Límites por minuto de cada política (los mismos valores del limitador en memoria).</summary>
public sealed record LimitesPorMinuto(int Global, int Auth, int Webhook, int Escritura, int Refresco)
{
    public int? De(string? politica) => politica switch
    {
        Politicas.LimiteAuth => Auth,
        Politicas.LimiteWebhook => Webhook,
        Politicas.LimiteEscritura => Escritura,
        Politicas.LimiteRefresco => Refresco,
        _ => null
    };
}

/// <summary>
/// Límite de peticiones COMPARTIDO entre todas las instancias de la API (ventana fija de 1 minuto en Redis).
/// El limitador de ASP.NET Core vive en la memoria de cada instancia: con 3 instancias, un atacante tendría 3 veces el
/// límite de intentos de login. Este middleware usa contadores en Redis (INCR + EXPIRE) para que el límite sea global.
/// Si Redis falla, deja pasar (el limitador en memoria sigue activo como respaldo): nunca tumba la API.
/// </summary>
public sealed class LimitadorDistribuidoMiddleware
{
    private readonly RequestDelegate _siguiente;
    private readonly IConnectionMultiplexer _redis;
    private readonly LimitesPorMinuto _limites;
    private readonly ILogger<LimitadorDistribuidoMiddleware> _log;
    private DateTime _ultimoAviso = DateTime.MinValue;

    public LimitadorDistribuidoMiddleware(RequestDelegate siguiente, IConnectionMultiplexer redis, LimitesPorMinuto limites,
        ILogger<LimitadorDistribuidoMiddleware> log)
    {
        _siguiente = siguiente; _redis = redis; _limites = limites; _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var politica = ctx.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        var minuto = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;

        try
        {
            var db = _redis.GetDatabase();
            if (await ExcedeAsync(db, $"trueke:rl:global:{ip}:{minuto}", _limites.Global) is { } espera1)
            {
                await RechazarAsync(ctx, espera1);
                return;
            }
            if (_limites.De(politica) is { } limite)
            {
                var particion = politica == Politicas.LimiteEscritura ? ctx.User.FindFirst("sub")?.Value ?? ip : ip;
                if (await ExcedeAsync(db, $"trueke:rl:{politica}:{particion}:{minuto}", limite) is { } espera2)
                {
                    await RechazarAsync(ctx, espera2);
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException or ObjectDisposedException)
        {
            if (DateTime.UtcNow - _ultimoAviso > TimeSpan.FromMinutes(1)) // no inundar el log si Redis está caído
            {
                _ultimoAviso = DateTime.UtcNow;
                _log.LogWarning(ex, "Limitador distribuido no disponible (Redis); se usa solo el limitador en memoria");
            }
        }
        await _siguiente(ctx);
    }

    /// <returns>Segundos de espera si se superó el límite; null si se puede continuar.</returns>
    private static async Task<int?> ExcedeAsync(IDatabase db, string clave, int limite)
    {
        var contador = await db.StringIncrementAsync(clave);
        if (contador == 1) await db.KeyExpireAsync(clave, TimeSpan.FromSeconds(70));
        if (contador <= limite) return null;
        return (int)(60 - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 60) + 1;
    }

    private static Task RechazarAsync(HttpContext ctx, int segundos)
    {
        ctx.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        return Problemas.EscribirAsync(ctx, StatusCodes.Status429TooManyRequests);
    }
}
