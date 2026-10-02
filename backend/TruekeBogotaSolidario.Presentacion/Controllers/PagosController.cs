using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/pagos")]
public sealed class PagosController : ControllerBase
{
    private const int MaxCuerpoWebhook = 100_000;
    private readonly IPagoService _pagos;
    public PagosController(IPagoService pagos) => _pagos = pagos;

    /// <summary>Valida, reserva los Eco-Puntos y devuelve lo necesario para abrir el checkout de la pasarela.</summary>
    [HttpPost("iniciar")]
    public async Task<ActionResult<PagoIniciadoDto>> Iniciar([FromBody] IniciarPagoRequest r, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await _pagos.IniciarAsync(User.IdActual(), r, ct));

    [HttpGet("{referencia}")]
    public async Task<ActionResult<PagoEstadoDto>> Estado(string referencia, CancellationToken ct)
        => Ok(await _pagos.ObtenerEstadoAsync(User.IdActual(), referencia, ct));

    /// <summary>Webhook de Wompi: se autentica por la firma del evento, no por JWT. Idempotente.</summary>
    [HttpPost("wompi/eventos"), AllowAnonymous, EnableRateLimiting(Politicas.LimiteWebhook)]
    public async Task<IActionResult> EventosWompi(CancellationToken ct)
    {
        if (Request.ContentLength is > MaxCuerpoWebhook) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        using var lector = new StreamReader(Request.Body, Encoding.UTF8, false, 1024, leaveOpen: true);
        var buffer = new char[MaxCuerpoWebhook + 1];
        var leidos = await lector.ReadBlockAsync(buffer.AsMemory(), ct);
        if (leidos > MaxCuerpoWebhook) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        await _pagos.ProcesarEventoWompiAsync(new string(buffer, 0, leidos), ct);
        return Ok();
    }

    /// <summary>Solo con Pagos:Proveedor=Simulado (desarrollo/pruebas). En producción responde 403.</summary>
    [HttpPost("{referencia}/simular")]
    public async Task<ActionResult<PagoEstadoDto>> Simular(string referencia, [FromQuery] bool aprobado = true)
        => Ok(await _pagos.SimularResultadoAsync(User.IdActual(), referencia, aprobado));
}
