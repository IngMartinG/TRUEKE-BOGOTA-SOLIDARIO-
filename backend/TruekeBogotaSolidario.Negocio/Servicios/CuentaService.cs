using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Reglas compartidas de verificación de cuenta.</summary>
public static class Guardas
{
    /// <summary>Publicar, solicitar, comentar, chatear, denunciar y pagar exigen haber demostrado que el correo es propio.</summary>
    public static void ExigirCorreoVerificado(Usuario u)
    {
        if (!u.CorreoVerificado)
            throw new AccesoDenegadoException("Verifica tu correo para realizar esta acción. Revisa tu bandeja de entrada o solicita un nuevo enlace.");
    }
}

/// <summary>Prepara los correos con enlaces de un solo uso (agrega el token; quien llama guarda y LUEGO encola).</summary>
public sealed class CorreosCuenta
{
    public static readonly TimeSpan VigenciaVerificacion = TimeSpan.FromHours(24);
    public static readonly TimeSpan VigenciaRestablecimiento = TimeSpan.FromMinutes(30);

    private readonly ITokenUsoUnicoRepository _tokens;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;

    public CorreosCuenta(ITokenUsoUnicoRepository tokens, IOptions<UrlsOpciones> urls, TimeProvider reloj)
    {
        _tokens = tokens; _urls = urls.Value; _reloj = reloj;
    }

    private async Task<string> NuevoTokenAsync(Usuario u, PropositoToken proposito, TimeSpan vigencia)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        await _tokens.InvalidarVigentesAsync(u.Id, proposito, ahora);
        var token = TokensSeguros.Generar();
        _tokens.Agregar(new TokenUsoUnico(u.Id, proposito, TokensSeguros.Hash(token), ahora, vigencia));
        return token;
    }

    private string Enlace(string ruta, string token) => $"{_urls.Frontend.TrimEnd('/')}/{ruta}?token={token}";

    public async Task<MensajeCorreo> PrepararVerificacionAsync(Usuario u)
    {
        var token = await NuevoTokenAsync(u, PropositoToken.VerificarCorreo, VigenciaVerificacion);
        return PlantillaCorreo.Crear(u.Correo, "Confirma tu correo en Trueke Bogotá Solidario",
            $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
            new[] { "¡Bienvenido a la comunidad! Para activar tu cuenta y empezar a publicar, intercambiar y donar, confirma tu correo. El enlace es válido por 24 horas." },
            ("Confirmar mi correo", Enlace("verificar-correo", token)),
            "Si no creaste una cuenta en Trueke Bogotá Solidario, ignora este mensaje.");
    }

    public async Task<MensajeCorreo> PrepararRestablecimientoAsync(Usuario u)
    {
        var token = await NuevoTokenAsync(u, PropositoToken.RestablecerClave, VigenciaRestablecimiento);
        return PlantillaCorreo.Crear(u.Correo, "Restablece tu contraseña de Trueke Bogotá Solidario",
            $"Hola {Mapeos.NombrePublico(u.NombreCompleto)}:",
            new[] { "Recibimos una solicitud para restablecer tu contraseña. El enlace es válido por 30 minutos y sirve una sola vez. Al cambiarla, se cerrarán tus sesiones abiertas." },
            ("Crear una nueva contraseña", Enlace("restablecer-clave", token)),
            "Si no fuiste tú, ignora este correo: tu contraseña no cambiará.");
    }
}

public interface ICuentaService
{
    Task VerificarCorreoAsync(string token);
    Task ReenviarVerificacionAsync(Guid actorId);
    /// <summary>Siempre termina igual (exista o no la cuenta): no permite averiguar qué correos están registrados.</summary>
    Task OlvideClaveAsync(string correo, string? captchaToken);
    Task RestablecerClaveAsync(string token, string claveNueva);
}

public sealed class CuentaService : ICuentaService
{
    private const int MaxEnlacesPorHora = 3;
    private const string EnlaceInvalido = "El enlace no es válido o ya expiró. Solicita uno nuevo.";

    private readonly IUsuarioRepository _usuarios;
    private readonly ITokenUsoUnicoRepository _tokens;
    private readonly ISesionRefreshRepository _refrescos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly CorreosCuenta _correos;
    private readonly ICorreoSaliente _salida;
    private readonly ISesionService _sesiones;
    private readonly TimeProvider _reloj;
    private readonly IVerificadorCaptcha _captcha;
    private readonly ILogger<CuentaService> _log;

    public CuentaService(IUsuarioRepository usuarios, ITokenUsoUnicoRepository tokens, ISesionRefreshRepository refrescos, IUnidadDeTrabajo uow,
        CorreosCuenta correos, ICorreoSaliente salida, ISesionService sesiones, TimeProvider reloj, IVerificadorCaptcha captcha,
        ILogger<CuentaService> log)
    {
        _captcha = captcha;
        _usuarios = usuarios; _tokens = tokens; _refrescos = refrescos; _uow = uow; _correos = correos; _salida = salida;
        _sesiones = sesiones; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private async Task<(TokenUsoUnico Token, Usuario Usuario)> ConsumirAsync(string token, PropositoToken proposito)
    {
        if (!TokensSeguros.FormatoValido(token)) throw new ReglaDeNegocioException(EnlaceInvalido);
        var t = await _tokens.ObtenerPorHashAsync(TokensSeguros.Hash(token), proposito);
        if (t is null || !t.EsValido(Ahora)) throw new ReglaDeNegocioException(EnlaceInvalido);
        var u = await _usuarios.ObtenerPorIdAsync(t.UsuarioId);
        if (u is null || u.EstaEliminado) throw new ReglaDeNegocioException(EnlaceInvalido);
        t.Consumir(Ahora);
        return (t, u);
    }

    public async Task VerificarCorreoAsync(string token)
    {
        var (_, u) = await ConsumirAsync(token, PropositoToken.VerificarCorreo);
        u.MarcarCorreoVerificado(Ahora);
        await _uow.GuardarCambiosAsync();
        _log.LogInformation("Correo verificado {UsuarioId}", u.Id);
    }

    public async Task ReenviarVerificacionAsync(Guid actorId)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        if (u.CorreoVerificado) throw new ReglaDeNegocioException("Tu correo ya está verificado.");
        if (await _tokens.ContarDesdeAsync(u.Id, PropositoToken.VerificarCorreo, Ahora.AddHours(-1)) >= MaxEnlacesPorHora)
            throw new ReglaDeNegocioException("Ya te enviamos varios enlaces. Revisa tu bandeja (y la carpeta de spam) o intenta en una hora.");
        var mensaje = await _correos.PrepararVerificacionAsync(u);
        await _uow.GuardarCambiosAsync();
        _salida.Encolar(mensaje);
    }

    public async Task OlvideClaveAsync(string correo, string? captchaToken)
    {
        await _captcha.ExigirAsync(captchaToken, AccionesCaptcha.OlvideClave);
        var u = await _usuarios.ObtenerPorCorreoCanonicoAsync(Usuario.CanonizarCorreo(correo));
        if (u is null || u.EstaEliminado) return;
        if (await _tokens.ContarDesdeAsync(u.Id, PropositoToken.RestablecerClave, Ahora.AddHours(-1)) >= MaxEnlacesPorHora) return;
        var mensaje = await _correos.PrepararRestablecimientoAsync(u);
        await _uow.GuardarCambiosAsync();
        _salida.Encolar(mensaje);
        _log.LogInformation("Enlace de restablecimiento emitido para {UsuarioId}", u.Id);
    }

    public async Task RestablecerClaveAsync(string token, string claveNueva)
    {
        var (_, u) = await ConsumirAsync(token, PropositoToken.RestablecerClave);
        u.EstablecerClave(claveNueva);       // sube VersionSeguridad: revoca los tokens de acceso
        u.RegistrarLoginExitoso();           // levanta un bloqueo por intentos fallidos
        u.MarcarCorreoVerificado(Ahora);     // demostró que el correo es suyo
        await _refrescos.RevocarTodasDelUsuarioAsync(u.Id, Ahora);
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
        _log.LogInformation("Contraseña restablecida {UsuarioId}", u.Id);
    }
}
