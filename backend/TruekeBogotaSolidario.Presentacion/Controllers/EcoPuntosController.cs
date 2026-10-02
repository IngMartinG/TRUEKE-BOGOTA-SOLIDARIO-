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

    /// <summary>Precio con descuento por Eco-Puntos para Destacar o Verificar (nunca llega a gratis).</summary>
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
