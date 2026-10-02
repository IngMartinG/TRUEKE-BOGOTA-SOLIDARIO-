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
    public AuthController(IAuthService auth) => _auth = auth;

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
    public UsuariosController(IAuthService auth) => _auth = auth;

    [HttpGet("yo")]
    public async Task<ActionResult<UsuarioDto>> Yo() => Ok(await _auth.ObtenerPerfilAsync(User.IdActual()));

    [HttpPut("yo")]
    public async Task<ActionResult<UsuarioDto>> ActualizarYo([FromBody] ActualizarPerfilRequest r)
        => Ok(await _auth.ActualizarPerfilAsync(User.IdActual(), r));
}
