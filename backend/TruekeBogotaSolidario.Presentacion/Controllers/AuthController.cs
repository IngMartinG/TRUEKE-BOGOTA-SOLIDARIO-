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

    [HttpPost("registrar"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<SesionDto>> Registrar([FromBody] RegistroRequest r)
        => StatusCode(StatusCodes.Status201Created, await _auth.RegistrarAsync(r));

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<SesionDto>> Login([FromBody] LoginRequest r) => Ok(await _auth.LoginAsync(r));

    [HttpPost("cambiar-clave")]
    public async Task<IActionResult> CambiarClave([FromBody] CambiarClaveRequest r)
    {
        await _auth.CambiarClaveAsync(User.IdActual(), r);
        return NoContent(); // todos los tokens anteriores (incluido el actual) quedan revocados: hay que volver a iniciar sesión
    }

    [HttpPost("cerrar-sesiones")]
    public async Task<IActionResult> CerrarSesiones()
    {
        await _auth.CerrarSesionesAsync(User.IdActual());
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
