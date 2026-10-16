using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/eco-puntos")]
public sealed class EcoPuntosController : ControllerBase
{
    private readonly IEcoPuntosService _eco;
    private readonly IPagoService _pagos;
    public EcoPuntosController(IEcoPuntosService eco, IPagoService pagos) { _eco = eco; _pagos = pagos; }

    /// <summary>Tabla de ganancias, beneficios y precios. Pública: un Invitado puede verla antes de registrarse.</summary>
    [HttpGet("politica"), AllowAnonymous]
    public ActionResult<PoliticaEcoPuntosDto> Politica() => Ok(_eco.ObtenerPolitica());

    [HttpGet("resumen")]
    public async Task<ActionResult<EcoPuntosResumenDto>> Resumen() => Ok(await _eco.ResumenAsync(User.IdActual()));

    /// <summary>Historial de Eco-Puntos de la persona (ganados, gastados, devueltos), del más reciente al más antiguo.</summary>
    [HttpGet("movimientos")]
    public async Task<ActionResult<PaginaDto<MovimientoEcoPuntosDto>>> Movimientos([FromQuery] PaginacionRequest p)
        => Ok(await _eco.MovimientosAsync(User.IdActual(), p.Pagina, p.Tamano));

    /// <summary>Precio de Destacar o Verificar con el descuento del plan y cuántos Eco-Puntos se pueden usar (la persona elige).</summary>
    [HttpGet("cotizacion")]
    public async Task<ActionResult<CotizacionDto>> Cotizar([FromQuery] ConceptoPagoDto concepto)
    {
        var c = await _pagos.CotizarAsync(User.IdActual(), concepto);
        if (c is not null) return Ok(c);
        return new ObjectResult(Problemas.Crear(HttpContext, StatusCodes.Status404NotFound, "Este concepto no admite descuento por Eco-Puntos."))
        {
            StatusCode = StatusCodes.Status404NotFound,
            ContentTypes = { Problemas.TipoContenido }
        };
    }
}
