using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly ICuentaService _cuenta;
    public AuthController(IAuthService auth, ICuentaService cuenta) { _auth = auth; _cuenta = cuenta; }

    /// <summary>Devuelve la sesión (token de acceso corto) y deja el token de refresco en una cookie HttpOnly.</summary>
    private SesionDto Iniciar(ResultadoAutenticacion r)
    {
        CookieRefresco.Escribir(HttpContext, r);
        return r.Sesion;
    }

    [HttpPost("registrar"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<SesionDto>> Registrar([FromBody] RegistroRequest r)
        => StatusCode(StatusCodes.Status201Created, Iniciar(await _auth.RegistrarAsync(r)));

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<SesionDto>> Login([FromBody] LoginRequest r) => Ok(Iniciar(await _auth.LoginAsync(r)));

    /// <summary>
    /// Inicio de sesión con Google: Angular obtiene el ID token con Google Identity Services y lo envía aquí.
    /// 201 si se creó la cuenta (exige aceptoPoliticaDatos), 200 si ya existía.
    /// </summary>
    [HttpPost("google"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<SesionDto>> LoginGoogle([FromBody] GoogleLoginRequest r)
    {
        var resultado = await _auth.LoginGoogleAsync(r);
        var sesion = Iniciar(resultado);
        return resultado.CuentaCreada ? StatusCode(StatusCodes.Status201Created, sesion) : Ok(sesion);
    }

    /// <summary>
    /// Nuevo token de acceso a partir de la cookie de refresco (que se rota). Requiere la cabecera X-Trueke-Csrf: 1.
    /// Angular lo llama al recibir un 401 y al recargar la página (el token de acceso vive solo en memoria).
    /// </summary>
    [HttpPost("refrescar"), AllowAnonymous, ProteccionCsrf, EnableRateLimiting(Politicas.LimiteRefresco)]
    public async Task<ActionResult<SesionDto>> Refrescar() => Ok(Iniciar(await _auth.RefrescarAsync(CookieRefresco.Leer(HttpContext))));

    /// <summary>Cierra la sesión de este dispositivo: revoca el refresco y borra la cookie.</summary>
    [HttpPost("salir"), AllowAnonymous, ProteccionCsrf, EnableRateLimiting(Politicas.LimiteRefresco)]
    public async Task<IActionResult> Salir()
    {
        await _auth.CerrarSesionAsync(CookieRefresco.Leer(HttpContext));
        CookieRefresco.Borrar(HttpContext);
        return NoContent();
    }

    /// <summary>Confirma el correo con el token del enlace enviado al registrarse (válido 24 h, un solo uso).</summary>
    [HttpPost("verificar-correo"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<IActionResult> VerificarCorreo([FromBody] TokenRequest r)
    {
        await _cuenta.VerificarCorreoAsync(r.Token);
        return NoContent();
    }

    [HttpPost("reenviar-verificacion"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<IActionResult> ReenviarVerificacion()
    {
        await _cuenta.ReenviarVerificacionAsync(User.IdActual());
        return Accepted();
    }

    /// <summary>Siempre 202, exista o no la cuenta (no permite averiguar qué correos están registrados).</summary>
    [HttpPost("olvide-clave"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<IActionResult> OlvideClave([FromBody] OlvideClaveRequest r)
    {
        await _cuenta.OlvideClaveAsync(r.Correo, r.CaptchaToken);
        return Accepted();
    }

    /// <summary>Define una contraseña nueva con el token del correo (30 min, un solo uso). Cierra todas las sesiones abiertas.</summary>
    [HttpPost("restablecer-clave"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<IActionResult> RestablecerClave([FromBody] RestablecerClaveRequest r)
    {
        await _cuenta.RestablecerClaveAsync(r.Token, r.ClaveNueva);
        return NoContent();
    }

    /// <summary>Cambia (o crea, si la cuenta es solo de Google) la contraseña. Revoca TODAS las sesiones y devuelve una nueva.</summary>
    [HttpPost("cambiar-clave")]
    public async Task<ActionResult<SesionDto>> CambiarClave([FromBody] CambiarClaveRequest r)
        => Ok(Iniciar(await _auth.CambiarClaveAsync(User.IdActual(), r)));

    [HttpPost("cerrar-sesiones")]
    public async Task<IActionResult> CerrarSesiones()
    {
        await _auth.CerrarSesionesAsync(User.IdActual());
        CookieRefresco.Borrar(HttpContext);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/v1/usuarios")]
public sealed class UsuariosController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IDatosPersonalesService _datos;
    private readonly IFotoPerfilService _foto;
    public UsuariosController(IAuthService auth, IDatosPersonalesService datos, IFotoPerfilService foto) { _auth = auth; _datos = datos; _foto = foto; }

    /// <summary>Foto de perfil: la URL es la de POST /archivos/subidas (tipo Imagen) tras subirla con PUT. Se guarda una copia limpia.</summary>
    [HttpPut("yo/foto"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<UsuarioDto>> CambiarFoto([FromBody] CambiarFotoRequest r)
        => Ok(await _foto.CambiarAsync(User.IdActual(), r.Url));

    [HttpDelete("yo/foto"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<UsuarioDto>> QuitarFoto() => Ok(await _foto.QuitarAsync(User.IdActual()));

    /// <summary>Ley 1581 — derecho de acceso: todos tus datos en JSON.</summary>
    [HttpGet("yo/datos"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<DatosPersonalesDto>> MisDatos() => Ok(await _datos.ExportarAsync(User.IdActual()));

    /// <summary>
    /// Ley 1581 — derecho de supresión: anonimiza la cuenta (irreversible). Exige "ELIMINAR" y la contraseña (o un ID token
    /// de Google reciente si la cuenta no tiene clave). Pagos y transacciones se conservan anonimizados por obligación contable.
    /// </summary>
    [HttpPost("yo/eliminar"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<IActionResult> EliminarCuenta([FromBody] EliminarCuentaRequest r)
    {
        await _datos.EliminarCuentaAsync(User.IdActual(), r);
        CookieRefresco.Borrar(HttpContext);
        return NoContent();
    }

    [HttpGet("yo")]
    public async Task<ActionResult<UsuarioDto>> Yo() => Ok(await _auth.ObtenerPerfilAsync(User.IdActual()));

    [HttpPut("yo")]
    public async Task<ActionResult<UsuarioDto>> ActualizarYo([FromBody] ActualizarPerfilRequest r)
        => Ok(await _auth.ActualizarPerfilAsync(User.IdActual(), r));

    /// <summary>Nombre comercial y NIT visibles en el perfil público (solo con el plan Empresa vigente).</summary>
    [HttpPut("yo/empresa"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<UsuarioDto>> ActualizarEmpresa([FromBody] PerfilEmpresaRequest r)
        => Ok(await _auth.ActualizarPerfilEmpresaAsync(User.IdActual(), r));
}
