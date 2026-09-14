using Microsoft.AspNetCore.Mvc;
using TruekeBogotaSolidario.Api.DTOs.Usuarios;
using TruekeBogotaSolidario.Api.Services.Interfaces;

namespace TruekeBogotaSolidario.Api.Controllers;

// ==========================================================================
// CAPA DE CONTROLADOR (equivalente al "AuthController" simplificado, sin
// login/OAuth, solo registro basico de Usuario para poder publicar/solicitar).
// ==========================================================================
[ApiController]
[Route("api/v1/usuarios")]
[Produces("application/json")]
public class UsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public UsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<UsuarioResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerTodos()
    {
        return Ok(await _usuarioService.ObtenerTodosAsync());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(Guid id)
    {
        return Ok(await _usuarioService.ObtenerPorIdAsync(id));
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var creado = await _usuarioService.CrearAsync(dto);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.Id }, creado);
    }
}
