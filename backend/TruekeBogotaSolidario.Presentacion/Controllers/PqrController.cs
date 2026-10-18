using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Peticiones, quejas, reclamos, sugerencias, retracto y reversión del pago (Ley 1755 de 2015 y Ley 1480 de 2011).</summary>
[ApiController]
[Authorize]
[PermitirSinFechaNacimiento] // reclamar o pedir ayuda nunca se bloquea
[Route("api/v1/pqr")]
public sealed class PqrController : ControllerBase
{
    private readonly IPqrService _pqr;
    public PqrController(IPqrService pqr) => _pqr = pqr;

    /// <summary>Radica la solicitud y envía el acuse con el número de radicado y la fecha límite de respuesta.</summary>
    [HttpPost, EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<PqrDto>> Crear([FromBody] CrearPqrRequest r)
        => StatusCode(StatusCodes.Status201Created, await _pqr.CrearAsync(User.IdActual(), r));

    [HttpGet("mias")]
    public async Task<ActionResult<IReadOnlyList<PqrDto>>> Mias() => Ok(await _pqr.ListarMiasAsync(User.IdActual()));

    /// <summary>
    /// PQRS pública, sin cuenta (autorizado por el dueño el 2026-10-18): captcha, límite por IP y por correo. El radicado y
    /// la respuesta llegan al correo indicado.
    /// </summary>
    [HttpPost("publica"), AllowAnonymous, EnableRateLimiting(Politicas.LimitePqrPublica)]
    public async Task<ActionResult<PqrDto>> CrearPublica([FromBody] CrearPqrPublicaRequest r)
        => StatusCode(StatusCodes.Status201Created, await _pqr.CrearPublicaAsync(r));

    /// <summary>Estado de una PQRS pública con su radicado y el correo con que se radicó (captcha y límite por IP).</summary>
    [HttpPost("consultar"), AllowAnonymous, EnableRateLimiting(Politicas.LimitePqrPublica)]
    public async Task<ActionResult<PqrDto>> Consultar([FromBody] ConsultarPqrRequest r) => Ok(await _pqr.ConsultarAsync(r));
}
