using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Asistente de ayuda con IA (solo cuentas con correo verificado). Si no está disponible, el front usa la ayuda local.</summary>
[ApiController]
[Authorize]
[Route("api/v1/asistente")]
public sealed class AsistenteController : ControllerBase
{
    private readonly IAsistenteService _asistente;
    public AsistenteController(IAsistenteService asistente) => _asistente = asistente;

    /// <summary>
    /// Responde una pregunta con el historial reciente que envía el front (no se guarda). 429 "limite_asistente" si se
    /// acabó el cupo diario; 503 "asistente_no_disponible" si la IA está apagada, sin cupo o no respondió.
    /// </summary>
    [HttpPost("preguntar"), EnableRateLimiting(Politicas.LimiteAsistente)]
    public async Task<ActionResult<RespuestaAsistenteDto>> Preguntar([FromBody] PreguntaAsistenteRequest r, CancellationToken ct)
        => Ok(await _asistente.ResponderAsync(User.IdActual(), r, ct));
}
