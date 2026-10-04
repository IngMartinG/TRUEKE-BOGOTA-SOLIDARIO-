using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Models;
using TruekeBogotaSolidario.Negocio;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Presentacion.Contrato;
using TruekeBogotaSolidario.Presentacion.Seguridad;
using TruekeBogotaSolidario.Presentacion.TiempoReal;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var esProduccion = builder.Environment.IsProduction();

// En Development, si no hay Jwt:Key en user-secrets se genera una efímera (los tokens dejan de valer al reiniciar).
// Fuera de Development NUNCA se genera: si falta, la validación de JwtOpciones impide arrancar.
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(config["Jwt:Key"]))
    config["Jwt:Key"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
// Igual con la llave que cifra los secretos de 2FA (en desarrollo, la 2FA configurada se pierde al reiniciar).
if (builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(config["Seguridad:ClaveCifrado"]))
    config["Seguridad:ClaveCifrado"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

// ---------- Fail-fast de configuración peligrosa ----------
if (esProduccion)
{
    if (string.Equals(config["Pagos:Proveedor"], "Simulado", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Pagos:Proveedor=Simulado no está permitido en Producción.");
    if (string.Equals(config["Database:Provider"], "InMemory", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Database:Provider=InMemory no está permitido en Producción.");
    if (config["Jwt:Key"]?.Contains("CAMBIAR", StringComparison.OrdinalIgnoreCase) == true)
        throw new InvalidOperationException("Jwt:Key es un placeholder. Define una clave real por variable de entorno o gestor de secretos.");
    if (string.IsNullOrWhiteSpace(config["Almacenamiento:ServicioUrl"]) && string.IsNullOrWhiteSpace(config["Almacenamiento:CadenaConexion"]))
        throw new InvalidOperationException("Configura Almacenamiento:ServicioUrl (Azure Blob con Managed Identity) para la subida de imágenes.");
    if (string.IsNullOrWhiteSpace(config["Seguridad:ClaveCifrado"]))
        throw new InvalidOperationException("Configura Seguridad:ClaveCifrado (32 bytes en base64: openssl rand -base64 32) para cifrar los secretos de 2FA.");
    if (string.IsNullOrWhiteSpace(config["Captcha:ClaveSecreta"]) || string.IsNullOrWhiteSpace(config["Captcha:ClaveSitio"]))
        throw new InvalidOperationException("Configura Captcha:ClaveSecreta y Captcha:ClaveSitio (reCAPTCHA v3) para proteger registro, login y recuperación de clave.");
    if (string.Equals(config["Correo:Proveedor"], "Simulado", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Correo:Proveedor=Simulado no está permitido en Producción (configura Correo:Smtp).");
    if (!Uri.TryCreate(config["Urls:Frontend"], UriKind.Absolute, out var front) || front.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("Urls:Frontend debe ser la URL https del front (los enlaces de los correos apuntan ahí).");
    if (string.Equals(config["Facturacion:Modo"], "Simulado", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Facturacion:Modo=Simulado no está permitido en Producción (usa Manual: el equipo registra número y CUFE de la DIAN).");
}

// ---------- Kestrel: sin cabecera Server, cuerpo máximo 1 MB ----------
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = TamanoMaximoCuerpo;
    k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});

// ---------- Monitoreo (Azure Application Insights vía OpenTelemetry): solo si hay cadena de conexión ----------
// Envía peticiones, dependencias (SQL, HTTP), excepciones y logs. La instrumentación de ASP.NET Core redacta los valores
// de la query string (p. ej. ?access_token= del hub). Nunca se registran cuerpos de peticiones ni contraseñas.
var appInsights = config["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? config["Monitoreo:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsights))
    builder.Services.AddOpenTelemetry().UseAzureMonitor(o => o.ConnectionString = appInsights);

// ---------- Negocio (que internamente registra Datos) ----------
builder.Services.AddNegocio(config);
builder.Services.AddAutenticacionJwt();

// ---------- SignalR (notificaciones en tiempo real) + backplane Redis opcional ----------
var signalR = builder.Services.AddSignalR(o =>
{
    o.EnableDetailedErrors = false;                    // nunca detalles internos al cliente
    o.MaximumReceiveMessageSize = 4 * 1024;            // el cliente solo invoca avisos mínimos (escribiendo / recibido)
}).AddJsonProtocol(o =>
{
    o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.PayloadSerializerOptions.Converters.Add(new FechaUtcJsonConverter());
});
var redisHabilitado = config.GetValue<bool>("Redis:Habilitado");
if (redisHabilitado)
{
    var redis = config["Redis:Conexion"];
    if (string.IsNullOrWhiteSpace(redis))
        throw new InvalidOperationException("Redis:Habilitado=true requiere Redis:Conexion (variable de entorno Redis__Conexion).");
    signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("trueke"));
    // Conexión compartida para el límite de peticiones distribuido (abortConnect=false: si Redis no está, la API arranca igual)
    var opcionesRedis = StackExchange.Redis.ConfigurationOptions.Parse(redis);
    opcionesRedis.AbortOnConnectFail = false;
    builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ => StackExchange.Redis.ConnectionMultiplexer.Connect(opcionesRedis));
    // "En línea" compartido entre instancias
    builder.Services.Replace(ServiceDescriptor.Singleton<IPresencia, PresenciaRedis>());
}
builder.Services.AddSingleton<IUserIdProvider, UsuarioIdPorSub>();
builder.Services.Replace(ServiceDescriptor.Singleton<IEmisorTiempoReal, EmisorSignalR>());

// ---------- MVC / JSON ----------
// Contrato con Angular: camelCase, enums como texto, fechas UTC ISO-8601 con "Z", errores ProblemDetails.
builder.Services.AddControllers(MensajesValidacionEspanol.Configurar)
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        o.JsonSerializerOptions.Converters.Add(new FechaUtcJsonConverter());
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
        o.AllowInputFormatterExceptionMessages = false; // no revelar tipos internos al fallar la deserialización
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = Problemas.Validacion; // 400 ProblemDetails con "errors" por campo
        o.ClientErrorMapping[StatusCodes.Status404NotFound].Title = Problemas.TituloPorDefecto(404);
    });
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    // Respuestas de error sin cuerpo (404 de ruta inexistente, 405, 415...): mismo formato y en español
    var estado = c.ProblemDetails.Status ?? c.HttpContext.Response.StatusCode;
    c.ProblemDetails.Type = Problemas.TipoPara(estado);
    c.ProblemDetails.Title = Problemas.TituloPorDefecto(estado);
    c.ProblemDetails.Detail = null;
    c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier;
});

// ---------- CORS: lista explícita de orígenes (nunca *) ----------
var origenes = config.GetSection("Cors:Origenes").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origenes.Length > 0)
        // AllowCredentials: el cliente JS de SignalR negocia con credenciales. Es seguro porque los orígenes son explícitos
        // (nunca "*") y la API no usa cookies: la autenticación viaja en el JWT.
        p.WithOrigins(origenes).WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "X-SignalR-User-Agent", ProteccionCsrfAttribute.Cabecera)
            .WithExposedHeaders("Location", "Retry-After")
            .AllowCredentials().SetPreflightMaxAge(TimeSpan.FromHours(1));
}));

// ---------- Rate limiting por IP (en memoria por instancia; con Redis, además, un límite global compartido) ----------
var limites = new LimitesPorMinuto(
    Global: config.GetValue("RateLimiting:GlobalPorMinuto", 300),
    Auth: config.GetValue("RateLimiting:AuthPorMinuto", 10),
    Webhook: config.GetValue("RateLimiting:WebhookPorMinuto", 120),
    Escritura: config.GetValue("RateLimiting:EscrituraPorMinuto", 20),
    Refresco: config.GetValue("RateLimiting:RefrescoPorMinuto", 60));
builder.Services.AddSingleton(limites);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            ctx.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Problemas.EscribirAsync(ctx.HttpContext, StatusCodes.Status429TooManyRequests, ct: ct);
    };
    static string Ip(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    var limiteAuth = limites.Auth;
    var limiteWebhook = limites.Webhook;
    var limiteGlobal = limites.Global;
    var limiteEscritura = limites.Escritura;
    var limiteRefresco = limites.Refresco;
    o.AddPolicy(Politicas.LimiteRefresco, c => RateLimitPartition.GetFixedWindowLimiter(Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteRefresco, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(Politicas.LimiteEscritura, c => RateLimitPartition.GetFixedWindowLimiter(
        c.User.FindFirst("sub")?.Value ?? Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteEscritura, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(Politicas.LimiteAuth, c => RateLimitPartition.GetFixedWindowLimiter(Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteAuth, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy(Politicas.LimiteWebhook, c => RateLimitPartition.GetFixedWindowLimiter(Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteWebhook, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c => RateLimitPartition.GetFixedWindowLimiter(Ip(c),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = limiteGlobal, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- Detrás de proxy / balanceador (Azure App Service, Nginx, Ingress): IP real del cliente ----------
// La IP real decide el límite de peticiones: solo se aceptan X-Forwarded-* de proxies conocidos. Si la API fuera
// alcanzable directamente con "confiar en todos", un atacante falsificaría X-Forwarded-For y evadiría el límite.
var redesConfiables = config.GetSection("Proxy:RedesConfiables").Get<string[]>() ?? Array.Empty<string>();   // CIDR, p. ej. 10.0.1.0/24
var proxiesConfiables = config.GetSection("Proxy:ProxiesConfiables").Get<string[]>() ?? Array.Empty<string>(); // IPs exactas
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = config.GetValue("Proxy:Saltos", 1);
    if (redesConfiables.Length > 0 || proxiesConfiables.Length > 0)
    {
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var red in redesConfiables)
        {
            var partes = red.Split('/');
            if (partes.Length != 2 || !IPAddress.TryParse(partes[0], out var ipRed) || !int.TryParse(partes[1], out var prefijo))
                throw new InvalidOperationException($"Proxy:RedesConfiables contiene un CIDR inválido: '{red}'.");
            o.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(ipRed, prefijo));
        }
        foreach (var proxy in proxiesConfiables)
            o.KnownProxies.Add(IPAddress.TryParse(proxy, out var ip) ? ip : throw new InvalidOperationException($"Proxy:ProxiesConfiables contiene una IP inválida: '{proxy}'."));
    }
    else if (config.GetValue<bool>("Proxy:Confiar"))
    {
        // Solo si la API NO es alcanzable directamente (App Service con restricciones de acceso / red privada).
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    }
});

builder.Services.AddHealthChecks();
builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = true; });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(o =>
    {
        o.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Trueke Bogotá Solidario API",
            Version = "v1",
            Description = "Errores: application/problem+json (title = mensaje mostrable, errors = validación por campo). " +
                          "Fechas en UTC ISO-8601 con Z. Enums como texto. Notificaciones en tiempo real: SignalR en /hubs/notificaciones " +
                          "(token por ?access_token=, evento \"notificacion\")."
        });
        o.SupportNonNullableReferenceTypes(); // los generadores de TypeScript distinguen string de string | null
        o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header,
            Description = "Pega el token devuelto por /auth/login (sin la palabra Bearer)."
        });
        o.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
        });
    });
}

var app = builder.Build();

await NegocioServiceCollectionExtensions.InicializarBaseDatosAsync(app.Services, config);

if (esProduccion)
{
    // Sin lista de hosts, se acepta CUALQUIER URL https como imagen/documento (píxeles de rastreo que filtran la IP de quien navega).
    if ((config.GetSection("Urls:HostsPermitidosImagenes").Get<string[]>() ?? Array.Empty<string>()).Length == 0)
        app.Logger.LogWarning("Urls:HostsPermitidosImagenes está vacío: configura el host de Azure Blob Storage (p. ej. tucuenta.blob.core.windows.net).");
    if ((config.GetSection("Urls:HostsPermitidosDocumentos").Get<string[]>() ?? Array.Empty<string>()).Length == 0)
        app.Logger.LogWarning("Urls:HostsPermitidosDocumentos está vacío: configura el host de Azure Blob Storage para los documentos de verificación.");
    if (origenes.Length == 0)
        app.Logger.LogWarning("Cors:Origenes está vacío: el front Angular no podrá llamar a la API desde el navegador.");
    if (string.IsNullOrWhiteSpace(appInsights))
        app.Logger.LogWarning("APPLICATIONINSIGHTS_CONNECTION_STRING no está definida: no habrá monitoreo de errores ni rendimiento.");
    if (config.GetValue<bool>("Proxy:Confiar") && redesConfiables.Length == 0 && proxiesConfiables.Length == 0)
        app.Logger.LogWarning("Proxy:Confiar=true sin Proxy:RedesConfiables: asegúrate de que la API solo sea alcanzable a través del proxy " +
                              "(restricciones de acceso de App Service), o se podrá falsificar X-Forwarded-For.");
    if (!redisHabilitado)
        app.Logger.LogWarning("Redis deshabilitado: el límite de peticiones es por instancia. Con varias instancias, habilita Redis.");
}

// ---------- Pipeline (el orden importa) ----------
app.UseForwardedHeaders();
app.UseMiddleware<ManejoErroresMiddleware>();
app.UseStatusCodePages(); // errores sin cuerpo (rutas inexistentes, 405...) → ProblemDetails
app.UseMiddleware<CabecerasSeguridadMiddleware>();
// Límite de cuerpo independiente del servidor (Kestrel ya lo aplica; esto cubre otros hosts y cuerpos declarados).
app.Use(async (ctx, siguiente) =>
{
    if (ctx.Request.ContentLength > TamanoMaximoCuerpo)
    {
        await Problemas.EscribirAsync(ctx, StatusCodes.Status413PayloadTooLarge);
        return;
    }
    var limite = ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (limite is { IsReadOnly: false }) limite.MaxRequestBodySize = TamanoMaximoCuerpo;
    await siguiente();
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();   // después de autenticar: la política "escritura" particiona por usuario
if (redisHabilitado) app.UseMiddleware<LimitadorDistribuidoMiddleware>(); // límite compartido entre instancias
app.UseAuthorization();

app.MapControllers();
// El hub cierra la conexión cuando expira el JWT con que se abrió (el cliente debe reconectar con un token nuevo).
app.MapHub<NotificacionesHub>(NotificacionesHub.Ruta, o => o.CloseOnAuthenticationExpiration = true);
// Liveness: el proceso responde. Readiness: además llega a la base de datos. No exponen detalles.
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapGet("/health/ready", async (TruekeBogotaSolidario.Negocio.ISaludSistema salud, CancellationToken ct) =>
    await salud.BaseDatosOkAsync(ct) ? Results.Ok(new { estado = "ok" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).AllowAnonymous();

app.Run();

public partial class Program
{
    /// <summary>1 MB: la API solo recibe JSON pequeño (las imágenes van a Blob Storage, aquí llega su URL).</summary>
    public const long TamanoMaximoCuerpo = 1_048_576;
}
