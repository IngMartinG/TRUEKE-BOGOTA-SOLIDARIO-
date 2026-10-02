using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Verificación en dos pasos con app autenticadora (Google/Microsoft Authenticator, Authy…).
/// Flujo: configurar (QR) → activar con el primer código (devuelve códigos de recuperación y una sesión nueva).
/// Obligatoria para usar las funciones de administración.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/auth/2fa")]
public sealed class DosFactoresController : ControllerBase
{
    private readonly IDosFactoresService _dosFactores;
    public DosFactoresController(IDosFactoresService dosFactores) => _dosFactores = dosFactores;

    [HttpPost("configurar"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<ConfiguracionDosFactoresDto>> Configurar() => Ok(await _dosFactores.IniciarAsync(User.IdActual()));

    /// <summary>Cierra las demás sesiones y devuelve una sesión nueva (con 2FA) más 10 códigos de recuperación que se muestran una sola vez.</summary>
    [HttpPost("activar"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<ActivacionDosFactoresDto>> Activar([FromBody] CodigoDosFactoresRequest r)
    {
        var (sesion, codigos) = await _dosFactores.ActivarAsync(User.IdActual(), r.Codigo);
        CookieRefresco.Escribir(HttpContext, sesion);
        return Ok(new ActivacionDosFactoresDto(codigos, sesion.Sesion));
    }

    [HttpPost("desactivar"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<SesionDto>> Desactivar([FromBody] CodigoDosFactoresRequest r)
    {
        var sesion = await _dosFactores.DesactivarAsync(User.IdActual(), r.Codigo);
        CookieRefresco.Escribir(HttpContext, sesion);
        return Ok(sesion.Sesion);
    }

    /// <summary>Genera 10 códigos nuevos (los anteriores dejan de servir). Exige un código de la app.</summary>
    [HttpPost("codigos-recuperacion"), EnableRateLimiting(Politicas.LimiteAuth)]
    public async Task<ActionResult<CodigosRecuperacionDto>> RegenerarCodigos([FromBody] CodigoDosFactoresRequest r)
        => Ok(new CodigosRecuperacionDto(await _dosFactores.RegenerarCodigosAsync(User.IdActual(), r.Codigo)));
}
