using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Pagos;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio;

public static class NegocioServiceCollectionExtensions
{
    /// <summary>Registra servicios de negocio y valida la configuración AL ARRANQUE (si falta un secreto, la app no inicia).</summary>
    public static IServiceCollection AddNegocio(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<JwtOpciones>().Bind(config.GetSection(JwtOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeguridadOpciones>().Bind(config.GetSection(SeguridadOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<UrlsOpciones>().Bind(config.GetSection(UrlsOpciones.Seccion));
        services.AddOptions<GoogleOpciones>().Bind(config.GetSection(GoogleOpciones.Seccion));
        services.AddSingleton<IValidadorGoogle, ValidadorGoogle>();
        services.AddOptions<LegalOpciones>().Bind(config.GetSection(LegalOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<PagosOpciones>().Bind(config.GetSection(PagosOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();

        var pagos = config.GetSection(PagosOpciones.Seccion).Get<PagosOpciones>() ?? new PagosOpciones();
        if (!pagos.EsSimulado)
        {
            var w = pagos.Wompi;
            if (string.IsNullOrWhiteSpace(w.LlavePublica) || string.IsNullOrWhiteSpace(w.SecretoIntegridad) || string.IsNullOrWhiteSpace(w.SecretoEventos))
                throw new InvalidOperationException("Pagos:Wompi requiere LlavePublica, SecretoIntegridad y SecretoEventos (o usa Pagos:Proveedor=Simulado solo en desarrollo).");
            if (!Uri.TryCreate(w.BaseUrl, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Pagos:Wompi:BaseUrl debe ser https.");
            services.AddHttpClient<IProveedorPagos, ProveedorPagosWompi>(c => c.Timeout = TimeSpan.FromSeconds(10));
        }
        else
        {
            services.AddSingleton<IProveedorPagos, ProveedorPagosSimulado>();
        }

        // Correo saliente: cola en memoria + worker; el transporte depende de la configuración.
        services.AddOptions<CorreoOpciones>().Bind(config.GetSection(CorreoOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        var correo = config.GetSection(CorreoOpciones.Seccion).Get<CorreoOpciones>() ?? new CorreoOpciones();
        if (correo.EsSimulado)
            services.AddScoped<ITransporteCorreo, TransporteCorreoSimulado>();
        else
        {
            if (string.IsNullOrWhiteSpace(correo.Smtp.Host))
                throw new InvalidOperationException("Correo:Smtp:Host es obligatorio (o usa Correo:Proveedor=Simulado solo en desarrollo).");
            services.AddScoped<ITransporteCorreo, TransporteCorreoSmtp>();
        }
        services.AddSingleton<ColaCorreo>();
        services.AddSingleton<ICorreoSaliente>(sp => sp.GetRequiredService<ColaCorreo>());
        services.AddHostedService<EnvioCorreosHostedService>();

        // Persistencia: Presentacion nunca referencia Datos directamente; todo entra por aquí.
        services.AddDatos(config["Database:Provider"] ?? "SqlServer", config.GetConnectionString("TruekeDb"), config["Database:NombreInMemory"]);
        services.AddScoped<ISaludSistema, SaludSistema>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEmisorTiempoReal, EmisorTiempoRealNulo>(); // Presentacion lo reemplaza por SignalR
        services.AddScoped<INotificador, NotificadorPersistente>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IArchivoService, ArchivoService>();

        // Archivos: Azure Blob si está configurado; si no, deshabilitado (las URLs se validan solo por host permitido)
        services.AddOptions<AlmacenamientoOpciones>().Bind(config.GetSection(AlmacenamientoOpciones.Seccion));
        var almacen = config.GetSection(AlmacenamientoOpciones.Seccion).Get<AlmacenamientoOpciones>() ?? new AlmacenamientoOpciones();
        if (almacen.Configurado) services.TryAddSingleton<IAlmacenArchivos, AlmacenBlobAzure>();
        else services.TryAddSingleton<IAlmacenArchivos, AlmacenDeshabilitado>();
        services.AddMemoryCache();

        services.AddSingleton<IGeneradorToken, GeneradorJwt>();
        services.AddScoped<ISesionService, SesionService>();
        services.AddScoped<EmisorSesiones>();
        services.AddScoped<CorreosCuenta>();
        services.AddScoped<ICuentaService, CuentaService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPublicacionService, PublicacionService>();
        services.AddScoped<ISolicitudService, SolicitudService>();
        services.AddScoped<IEcoPuntosService, EcoPuntosService>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IAdministracionService, AdministracionService>();
        services.AddScoped<IComentarioService, ComentarioService>();
        services.AddHostedService<ReconciliadorPagosHostedService>();
        return services;
    }

    /// <summary>Crea/migra el esquema y siembra el SuperUsuario inicial (solo si no existe y hay credenciales en configuración).</summary>
    public static Task InicializarBaseDatosAsync(IServiceProvider proveedor, IConfiguration config)
        => DatosInicializador.InicializarAsync(proveedor, config["Database:Inicializacion"] ?? "None",
            config["Bootstrap:SuperUsuarioCorreo"], config["Bootstrap:SuperUsuarioClave"]);
}

public interface ISaludSistema
{
    Task<bool> BaseDatosOkAsync(CancellationToken ct);
}

internal sealed class SaludSistema : ISaludSistema
{
    private readonly ISaludBaseDatos _bd;
    public SaludSistema(ISaludBaseDatos bd) => _bd = bd;
    public Task<bool> BaseDatosOkAsync(CancellationToken ct) => _bd.PuedeConectarAsync(ct);
}
