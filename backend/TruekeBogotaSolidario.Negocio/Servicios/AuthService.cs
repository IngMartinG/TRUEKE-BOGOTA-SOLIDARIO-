using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IAuthService
{
    Task<SesionDto> RegistrarAsync(RegistroRequest r);
    Task<SesionDto> LoginAsync(LoginRequest r);
    Task<UsuarioDto> ObtenerPerfilAsync(Guid actorId);
    Task<UsuarioDto> ActualizarPerfilAsync(Guid actorId, ActualizarPerfilRequest r);
    Task CambiarClaveAsync(Guid actorId, CambiarClaveRequest r);
    /// <summary>Cierra la sesión en TODOS los dispositivos (invalida todos los tokens emitidos).</summary>
    Task CerrarSesionesAsync(Guid actorId);
}

public sealed class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IGeneradorToken _tokens;
    private readonly ISesionService _sesiones;
    private readonly SeguridadOpciones _seg;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AuthService> _log;

    public AuthService(IUsuarioRepository usuarios, IUnidadDeTrabajo uow, IGeneradorToken tokens, ISesionService sesiones,
        IOptions<SeguridadOpciones> seg, TimeProvider reloj, ILogger<AuthService> log)
    {
        _usuarios = usuarios; _uow = uow; _tokens = tokens; _sesiones = sesiones; _seg = seg.Value; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    private SesionDto CrearSesion(Usuario u)
    {
        var (token, expira) = _tokens.Generar(u);
        return new SesionDto(token, expira, Mapeos.AUsuarioDto(u, Ahora));
    }

    public async Task<SesionDto> RegistrarAsync(RegistroRequest r)
    {
        var correo = Usuario.NormalizarCorreo(r.Correo);
        if (await _usuarios.ExisteCorreoAsync(correo))
            throw new ReglaDeNegocioException("Ya existe una cuenta con ese correo.");

        var usuario = new Usuario(r.NombreCompleto, r.Localidad, correo, r.Clave);
        usuario.AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosBienvenida);
        _usuarios.Agregar(usuario);
        await _uow.GuardarCambiosAsync(); // el índice único del correo cubre el registro simultáneo
        _log.LogInformation("Usuario registrado {UsuarioId}", usuario.Id);
        return CrearSesion(usuario);
    }

    public async Task<SesionDto> LoginAsync(LoginRequest r)
    {
        var ahora = Ahora;
        var usuario = await _usuarios.ObtenerPorCorreoAsync(Usuario.NormalizarCorreo(r.Correo));

        if (usuario is null)
        {
            PasswordHasher.VerificarSenuelo(r.Clave); // mismo costo de tiempo: no se puede enumerar correos
            throw new AutenticacionException();
        }

        if (usuario.EstaBloqueado(ahora))
        {
            PasswordHasher.VerificarSenuelo(r.Clave);
            _log.LogWarning("Intento de login sobre cuenta bloqueada {UsuarioId}", usuario.Id);
            throw new AutenticacionException(); // mensaje idéntico: no se revela que está bloqueada ni por cuánto tiempo
        }

        if (!usuario.VerificarClave(r.Clave))
        {
            usuario.RegistrarLoginFallido(_seg.MaxIntentosLogin, TimeSpan.FromMinutes(_seg.MinutosBloqueo), ahora);
            try { await _uow.GuardarCambiosAsync(); }
            catch (ConflictoDeConcurrenciaException) { /* otro intento simultáneo ya actualizó el contador */ }
            throw new AutenticacionException();
        }

        if (usuario.IntentosFallidosLogin != 0 || usuario.BloqueadoHasta.HasValue)
        {
            usuario.RegistrarLoginExitoso();
            await _uow.GuardarCambiosAsync();
        }
        return CrearSesion(usuario);
    }

    private async Task<Usuario> CargarAsync(Guid id)
        => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    public async Task<UsuarioDto> ObtenerPerfilAsync(Guid actorId)
        => Mapeos.AUsuarioDto(await CargarAsync(actorId), Ahora);

    public async Task<UsuarioDto> ActualizarPerfilAsync(Guid actorId, ActualizarPerfilRequest r)
    {
        var u = await CargarAsync(actorId);
        u.ActualizarPerfil(r.NombreCompleto, r.Localidad);
        await _uow.GuardarCambiosAsync();
        return Mapeos.AUsuarioDto(u, Ahora);
    }

    public async Task CambiarClaveAsync(Guid actorId, CambiarClaveRequest r)
    {
        var u = await CargarAsync(actorId);
        if (!u.VerificarClave(r.ClaveActual)) throw new ReglaDeNegocioException("La contraseña actual no es correcta.");
        if (r.ClaveActual == r.ClaveNueva) throw new ReglaDeNegocioException("La nueva contraseña debe ser distinta de la actual.");
        u.EstablecerClave(r.ClaveNueva); // sube VersionSeguridad: todos los tokens anteriores quedan revocados
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
        _log.LogInformation("Contraseña cambiada {UsuarioId}", u.Id);
    }

    public async Task CerrarSesionesAsync(Guid actorId)
    {
        var u = await CargarAsync(actorId);
        u.InvalidarSesiones();
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
    }
}
