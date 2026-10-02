using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;

namespace TruekeBogotaSolidario.Presentacion.Controllers;

/// <summary>
/// Los archivos NO pasan por la API: aquí se obtiene una autorización (SAS) de 5 minutos para subir UN archivo directo a
/// Azure Blob. Después se envía la URL resultante (p. ej. en imagenUrl al crear la publicación) y la API verifica que el
/// archivo exista, sea del usuario, pese ≤ 5 MB y su contenido real sea del tipo declarado.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/archivos")]
public sealed class ArchivosController : ControllerBase
{
    private readonly IArchivoService _archivos;
    public ArchivosController(IArchivoService archivos) => _archivos = archivos;

    [HttpPost("subidas"), EnableRateLimiting(Politicas.LimiteEscritura)]
    public async Task<ActionResult<SubidaArchivoDto>> SolicitarSubida([FromBody] SolicitarSubidaRequest r, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await _archivos.SolicitarSubidaAsync(User.IdActual(), r, ct));
}
