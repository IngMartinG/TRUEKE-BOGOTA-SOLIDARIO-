using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>Configuración pública para el front (clave de sitio de reCAPTCHA, Client ID de Google, límites). Sin secretos.</summary>
[ApiController]
[Authorize]
[Route("api/v1/configuracion")]
public sealed class ConfiguracionController : ControllerBase
{
    private readonly IConfiguracionService _configuracion;
    public ConfiguracionController(IConfiguracionService configuracion) => _configuracion = configuracion;

    [HttpGet, AllowAnonymous]
    public ActionResult<ConfiguracionPublicaDto> Obtener() => Ok(_configuracion.ObtenerPublica());
}
