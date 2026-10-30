using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Asistente;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Moderacion;
using TruekeBogotaSolidario.Negocio.Pagos;
using TruekeBogotaSolidario.Negocio.Push;
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
        services.AddOptions<CaptchaOpciones>().Bind(config.GetSection(CaptchaOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.AddHttpClient<IVerificadorCaptcha, VerificadorRecaptcha>(c => c.Timeout = TimeSpan.FromSeconds(5));
        services.AddOptions<GoogleOpciones>().Bind(config.GetSection(GoogleOpciones.Seccion));
        services.AddSingleton<IValidadorGoogle, ValidadorGoogle>();
        services.AddOptions<LegalOpciones>().Bind(config.GetSection(LegalOpciones.Seccion)).ValidateDataAnnotations()
            .Validate(o => o.CorreoContactoValido, "Legal:CorreoContacto no es un correo válido.").ValidateOnStart();
        services.AddOptions<PagosOpciones>().Bind(config.GetSection(PagosOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DonacionesOpciones>().Bind(config.GetSection(DonacionesOpciones.Seccion))
            .Validate(o => o.UrlValida, "Donaciones:Url debe ser una URL https.").ValidateOnStart();
        services.AddOptions<FacturacionOpciones>().Bind(config.GetSection(FacturacionOpciones.Seccion)).ValidateDataAnnotations()
            .Validate(o => o.EsSimulado || string.Equals(o.Modo, "Manual", StringComparison.OrdinalIgnoreCase),
                "Facturacion:Modo debe ser 'Manual' o 'Simulado'.")
            .ValidateOnStart();

        var pagos = config.GetSection(PagosOpciones.Seccion).Get<PagosOpciones>() ?? new PagosOpciones();
        if (!pagos.EsSimulado)
        {
            var w = pagos.Wompi;
            // Sin la llave privada, la consulta de transacciones a Wompi falla: los pagos pendientes nunca se resolverían
            // (ni vencerían devolviendo los puntos reservados).
            if (string.IsNullOrWhiteSpace(w.LlavePublica) || string.IsNullOrWhiteSpace(w.LlavePrivada)
                || string.IsNullOrWhiteSpace(w.SecretoIntegridad) || string.IsNullOrWhiteSpace(w.SecretoEventos))
                throw new InvalidOperationException("Pagos:Wompi requiere LlavePublica, LlavePrivada, SecretoIntegridad y SecretoEventos (o usa Pagos:Proveedor=Simulado solo en desarrollo).");
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
        else if (correo.EsBrevo)
        {
            if (string.IsNullOrWhiteSpace(correo.Brevo.ClaveApi))
                throw new InvalidOperationException("Correo:Brevo:ClaveApi es obligatoria con Correo:Proveedor=Brevo (variable Correo__Brevo__ClaveApi).");
            // Sin seguir redirecciones: la clave API solo viaja a api.brevo.com. Los reintentos los hace EnvioCorreosHostedService.
            services.AddHttpClient(TransporteCorreoBrevo.ClienteHttp, c => c.Timeout = TimeSpan.FromSeconds(15))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddScoped<ITransporteCorreo, TransporteCorreoBrevo>();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(correo.Smtp.Host))
                throw new InvalidOperationException("Correo:Smtp:Host es obligatorio (o usa Correo:Proveedor=Simulado solo en desarrollo).");
            services.AddScoped<ITransporteCorreo, TransporteCorreoSmtp>();
        }
        // Notificaciones push (Web Push + VAPID): sin llaves, apagadas. Solo se habla con los servicios de push permitidos.
        services.AddOptions<PushOpciones>().Bind(config.GetSection(PushOpciones.Seccion))
            .Validate(o => o.Validas, "Push:ClavePublica/ClavePrivada no son llaves VAPID P-256 válidas (base64url, 65 y 32 bytes).")
            .ValidateOnStart();
        services.AddHttpClient(EnvioPushHostedService.ClienteHttp, c => c.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<ColaPush>();
        services.AddScoped<IPushService, PushService>();
        services.AddScoped<IAvisosPush, AvisosPush>();
        services.AddHostedService<EnvioPushHostedService>();

        // Asistente de ayuda con IA (Groq, nivel gratis): apagado por defecto; sin él, el front responde con la ayuda local.
        services.AddOptions<AsistenteOpciones>().Bind(config.GetSection(AsistenteOpciones.Seccion)).ValidateDataAnnotations()
            .Validate(o => o.EsGroq || string.Equals(o.Proveedor, "Desactivado", StringComparison.OrdinalIgnoreCase),
                "Asistente:Proveedor debe ser 'Groq' o 'Desactivado'.")
            .ValidateOnStart();
        var asistente = config.GetSection(AsistenteOpciones.Seccion).Get<AsistenteOpciones>() ?? new AsistenteOpciones();
        if (asistente.EsGroq)
        {
            if (string.IsNullOrWhiteSpace(asistente.Groq.ClaveApi))
                throw new InvalidOperationException("Asistente:Groq:ClaveApi es obligatoria con Asistente:Proveedor=Groq (variable Asistente__Groq__ClaveApi).");
            if (!Uri.TryCreate(asistente.Groq.Url, UriKind.Absolute, out var urlGroq) || urlGroq.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("Asistente:Groq:Url debe ser https.");
            // Sin seguir redirecciones: la llave solo viaja al host configurado.
            services.AddHttpClient(ModeloGroq.ClienteHttp, c => c.Timeout = TimeSpan.FromSeconds(15))
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
            services.AddSingleton<IModeloLenguaje, ModeloGroq>();
        }
        else
        {
            services.AddSingleton<IModeloLenguaje, ModeloDesactivado>();
        }
        services.AddScoped<IAsistenteService, AsistenteService>();

        services.AddSingleton<ColaCorreo>();
        services.AddSingleton<ICorreoSaliente>(sp => sp.GetRequiredService<ColaCorreo>());
        services.AddHostedService<EnvioCorreosHostedService>();

        // Persistencia: Presentacion nunca referencia Datos directamente; todo entra por aquí.
        services.AddDatos(config["Database:Provider"] ?? "PostgreSQL", config.GetConnectionString("TruekeDb"), config["Database:NombreInMemory"]);
        services.AddScoped<ISaludSistema, SaludSistema>();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEmisorTiempoReal, EmisorTiempoRealNulo>(); // Presentacion lo reemplaza por SignalR
        services.TryAddSingleton<IPresencia, PresenciaEnMemoria>();
        services.TryAddScoped<ISesionModeracion, SesionModeracionSinPeticion>(); // Presentacion la lee del JWT de la petición          // con Redis, Presentacion usa la distribuida
        services.AddScoped<INotificador, NotificadorPersistente>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IFotoPerfilService, FotoPerfilService>();
        services.AddScoped<IBloqueoService, BloqueoService>();
        services.AddSingleton<LimitadorAvisosCorreo>();
        services.AddScoped<IAvisosCorreo, AvisosCorreo>();
        services.AddScoped<IArchivoService, ArchivoService>();
        services.AddScoped<IDenunciaService, DenunciaService>();
        services.AddOptions<ModeracionOpciones>().Bind(config.GetSection(ModeracionOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<IModeracionPublicacionesService, ModeracionPublicacionesService>();
        services.AddScoped<IDatosPersonalesService, DatosPersonalesService>();
        services.AddScoped<IConfiguracionService, ConfiguracionService>();
        services.AddScoped<IVistaPreviaService, VistaPreviaService>();
        services.AddSingleton<ICifradorSecretos, CifradorAesGcm>();
        services.AddScoped<VerificadorDosFactores>();
        services.AddScoped<ConfirmadorIdentidad>();
        services.AddScoped<IDosFactoresService, DosFactoresService>();

        // Archivos: Supabase Storage si está configurado; si no, deshabilitado (las URLs se validan solo por host permitido)
        services.AddOptions<AlmacenamientoOpciones>().Bind(config.GetSection(AlmacenamientoOpciones.Seccion))
            .Validate(o => o.UrlValida, "Almacenamiento:Url debe ser https (o http solo hacia este equipo) y sin query.")
            .Validate(o => o.CdnValida, "Almacenamiento:CdnUrl debe ser https, sin query, y requiere Almacenamiento:Url.")
            .ValidateOnStart();
        var almacen = config.GetSection(AlmacenamientoOpciones.Seccion).Get<AlmacenamientoOpciones>() ?? new AlmacenamientoOpciones();
        if (almacen.Configurado) services.TryAddSingleton<IAlmacenArchivos, AlmacenSupabase>();
        else services.TryAddSingleton<IAlmacenArchivos, AlmacenDeshabilitado>();
        // Storage: sin seguir redirecciones (la llave secreta solo va al host configurado).
        services.AddHttpClient(AlmacenSupabase.ClienteHttp, c => c.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<MiniaturasHostedService>();
        // Foto de Google: sin seguir redirecciones (una redirección podría llevar a un host interno: SSRF).
        services.AddHttpClient(ImportadorFotoGoogle.ClienteHttp, c => c.Timeout = TimeSpan.FromSeconds(8))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IImportadorFotoPerfil, ImportadorFotoGoogle>();
        // Foto de perfil real: detector de rostros local y revisión de las fotos anteriores a la regla.
        services.AddOptions<FotosPerfilOpciones>().Bind(config.GetSection(FotosPerfilOpciones.Seccion)).ValidateDataAnnotations().ValidateOnStart();
        services.TryAddSingleton<IDetectorRostros, DetectorRostrosOnnx>();
        services.AddScoped<IRevisorFotoPerfil, RevisorFotoPerfil>();
        services.AddHostedService<RevisionFotosPerfilHostedService>();
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
        services.AddScoped<IBonosEcoPuntosService, BonosEcoPuntosService>();
        services.AddScoped<IImpactoService, ImpactoService>();
        services.AddScoped<IPagoService, PagoService>();
        services.AddScoped<IAdministracionService, AdministracionService>();
        services.AddScoped<IComentarioService, ComentarioService>();
        services.AddSingleton<IUbicacionService, UbicacionService>();
        services.AddScoped<IFacturacionService, FacturacionService>();
        services.AddScoped<IPqrService, PqrService>();
        services.AddScoped<IOferenteService, OferenteService>();
        services.AddSingleton<ResumenPqrPublicas>();
        services.AddHostedService<ResumenPqrPublicasHostedService>();
        services.AddScoped<IBusquedaGuardadaService, BusquedaGuardadaService>();
        services.AddScoped<IDeseoService, DeseoService>();
        services.AddScoped<IFundacionService, FundacionService>();
        services.AddScoped<IEmpresaService, EmpresaService>();
        services.AddScoped<INegocioService, NegocioService>();
        services.AddSingleton<IRegistroVistas, RegistroVistasEnMemoria>();
        services.AddHostedService<VolcadoVistasHostedService>();
        services.AddHostedService<ReconciliadorPagosHostedService>();
        services.AddHostedService<MantenimientoHostedService>();
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
