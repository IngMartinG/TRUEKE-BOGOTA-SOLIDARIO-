using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Datos para la factura electrónica y facturas del usuario. Sin datos guardados se factura a "consumidor final".</summary>
[ApiController]
[Authorize]
[Route("api/v1/cuenta")]
public sealed class FacturacionController : ControllerBase
{
    private readonly IFacturacionService _facturacion;
    public FacturacionController(IFacturacionService facturacion) => _facturacion = facturacion;

    [HttpGet("facturacion")]
    public async Task<ActionResult<DatosFacturacionDto>> Datos() => Ok(await _facturacion.ObtenerDatosAsync(User.IdActual()));

    [HttpPut("facturacion"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<DatosFacturacionDto>> Actualizar([FromBody] DatosFacturacionRequest r)
        => Ok(await _facturacion.ActualizarDatosAsync(User.IdActual(), r));

    [HttpDelete("facturacion")]
    public async Task<IActionResult> Borrar()
    {
        await _facturacion.BorrarDatosAsync(User.IdActual());
        return NoContent();
    }

    [HttpGet("facturas")]
    public async Task<ActionResult<IReadOnlyList<FacturaDto>>> Facturas() => Ok(await _facturacion.ListarMiasAsync(User.IdActual()));
}
