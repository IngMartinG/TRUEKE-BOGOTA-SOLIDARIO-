using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Chat privado entre las dos partes de una solicitud. Los mensajes nuevos de la contraparte llegan también en tiempo real
/// por el hub (evento "mensaje"). Una conversación ajena responde 404.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/conversaciones")]
public sealed class ConversacionesController : ControllerBase
{
    private readonly IChatService _chat;
    public ConversacionesController(IChatService chat) => _chat = chat;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversacionDto>>> Listar() => Ok(await _chat.ListarAsync(User.IdActual()));

    [HttpGet("{id:guid}/mensajes")]
    public async Task<ActionResult<IReadOnlyList<MensajeChatDto>>> Mensajes(Guid id, [FromQuery] MensajesRequest r)
        => Ok(await _chat.ListarMensajesAsync(User.IdActual(), id, r.AntesDe, r.Tamano));

    [HttpPost("{id:guid}/mensajes"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<MensajeChatDto>> Enviar(Guid id, [FromBody] EnviarMensajeRequest r)
        => StatusCode(StatusCodes.Status201Created, await _chat.EnviarAsync(User.IdActual(), id, r.Texto, r.RespuestaAId));

    [HttpPost("{id:guid}/leer")]
    public async Task<IActionResult> Leer(Guid id)
    {
        await _chat.MarcarLeidosAsync(User.IdActual(), id);
        return NoContent();
    }
}
