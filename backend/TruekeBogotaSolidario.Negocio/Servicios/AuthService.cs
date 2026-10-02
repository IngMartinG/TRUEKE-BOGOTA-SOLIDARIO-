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
    Task<ResultadoAutenticacion> RegistrarAsync(RegistroRequest r);
    Task<ResultadoAutenticacion> LoginAsync(LoginRequest r);
    /// <summary>Rota el token de refresco. Reusar uno ya reemplazado revoca toda la familia (posible robo).</summary>
    Task<ResultadoAutenticacion> RefrescarAsync(string? tokenRefresco);
    /// <summary>Cierra la sesión de ESTE dispositivo (revoca la familia del token de refresco). Nunca falla.</summary>
    Task CerrarSesionAsync(string? tokenRefresco);
    Task<UsuarioDto> ObtenerPerfilAsync(Guid actorId);
    Task<UsuarioDto> ActualizarPerfilAsync(Guid actorId, ActualizarPerfilRequest r);
    /// <summary>Revoca todas las sesiones y devuelve una nueva para el dispositivo actual.</summary>
    Task<ResultadoAutenticacion> CambiarClaveAsync(Guid actorId, CambiarClaveRequest r);
    /// <summary>Cierra la sesión en TODOS los dispositivos (invalida todos los tokens emitidos).</summary>
    Task CerrarSesionesAsync(Guid actorId);
}

public sealed class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ISesionRefreshRepository _refrescos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly EmisorSesiones _emisor;
    private readonly ISesionService _sesiones;
    private readonly SeguridadOpciones _seg;
    private readonly LegalOpciones _legal;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AuthService> _log;

    public AuthService(IUsuarioRepository usuarios, ISesionRefreshRepository refrescos, IUnidadDeTrabajo uow, EmisorSesiones emisor,
        ISesionService sesiones, IOptions<SeguridadOpciones> seg, IOptions<LegalOpciones> legal, TimeProvider reloj, ILogger<AuthService> log)
    {
        _usuarios = usuarios; _refrescos = refrescos; _uow = uow; _emisor = emisor; _sesiones = sesiones; _seg = seg.Value; _legal = legal.Value; _reloj = reloj; _log = log;
    }

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<ResultadoAutenticacion> RegistrarAsync(RegistroRequest r)
    {
        var correo = Usuario.NormalizarCorreo(r.Correo);
        if (await _usuarios.ExisteCorreoAsync(correo))
            throw new ReglaDeNegocioException("Ya existe una cuenta con ese correo.");

        var usuario = new Usuario(r.NombreCompleto, r.Localidad, correo, r.Clave);
        usuario.AceptarPoliticaDatos(_legal.VersionPoliticaDatos, Ahora);
        usuario.AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosBienvenida);
        _usuarios.Agregar(usuario);
        var sesion = _emisor.Emitir(usuario);
        await _uow.GuardarCambiosAsync(); // el índice único del correo cubre el registro simultáneo
        _log.LogInformation("Usuario registrado {UsuarioId}", usuario.Id);
        return sesion;
    }

    public async Task<ResultadoAutenticacion> LoginAsync(LoginRequest r)
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

        usuario.RegistrarLoginExitoso();
        var sesion = _emisor.Emitir(usuario);
        await _uow.GuardarCambiosAsync();
        return sesion;
    }

    public async Task<ResultadoAutenticacion> RefrescarAsync(string? tokenRefresco)
    {
        const string expirada = "Tu sesión expiró. Inicia sesión de nuevo.";
        if (!TokensSeguros.FormatoValido(tokenRefresco)) throw new AutenticacionException(expirada);

        var ahora = Ahora;
        var actual = await _refrescos.ObtenerPorHashAsync(TokensSeguros.Hash(tokenRefresco!)) ?? throw new AutenticacionException(expirada);

        if (actual.ReemplazadoPorId is not null)
        {
            // Dos pestañas refrescando a la vez: el navegador ya recibió el token nuevo, basta con reintentar.
            if (actual.RevocadoUtc > ahora.AddSeconds(-_seg.SegundosGraciaRefresco))
                throw new ConflictoDeConcurrenciaException("La sesión se renovó en otra pestaña. Vuelve a intentarlo.");

            // Reuso de un token viejo: alguien más lo tiene. Se corta toda la sesión (también al atacante).
            await _refrescos.RevocarFamiliaAsync(actual.FamiliaId, ahora);
            await _uow.GuardarCambiosAsync();
            _log.LogWarning("Reuso de token de refresco detectado para {UsuarioId}: familia {FamiliaId} revocada", actual.UsuarioId, actual.FamiliaId);
            throw new AutenticacionException(expirada);
        }

        if (!actual.EstaActiva(ahora)) throw new AutenticacionException(expirada);

        var usuario = await _usuarios.ObtenerPorIdAsync(actual.UsuarioId);
        if (usuario is null || usuario.EstaEliminado) throw new AutenticacionException(expirada);
        var sesion = _emisor.Emitir(usuario, actual);
        await _uow.GuardarCambiosAsync(); // RowVersion: dos refrescos simultáneos del mismo token → uno recibe 409
        return sesion;
    }

    public async Task CerrarSesionAsync(string? tokenRefresco)
    {
        if (!TokensSeguros.FormatoValido(tokenRefresco)) return;
        var actual = await _refrescos.ObtenerPorHashAsync(TokensSeguros.Hash(tokenRefresco!));
        if (actual is null) return;
        await _refrescos.RevocarFamiliaAsync(actual.FamiliaId, Ahora);
        await _uow.GuardarCambiosAsync();
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

    public async Task<ResultadoAutenticacion> CambiarClaveAsync(Guid actorId, CambiarClaveRequest r)
    {
        var u = await CargarAsync(actorId);
        if (u.TieneClave)
        {
            if (string.IsNullOrEmpty(r.ClaveActual) || !u.VerificarClave(r.ClaveActual))
                throw new ReglaDeNegocioException("La contraseña actual no es correcta.");
            if (r.ClaveActual == r.ClaveNueva) throw new ReglaDeNegocioException("La nueva contraseña debe ser distinta de la actual.");
        }
        u.EstablecerClave(r.ClaveNueva); // sube VersionSeguridad: todos los tokens de acceso anteriores quedan revocados
        await _refrescos.RevocarTodasDelUsuarioAsync(u.Id, Ahora);
        var sesion = _emisor.Emitir(u);
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
        _log.LogInformation("Contraseña cambiada {UsuarioId}", u.Id);
        return sesion;
    }

    public async Task CerrarSesionesAsync(Guid actorId)
    {
        var u = await CargarAsync(actorId);
        u.InvalidarSesiones();
        await _refrescos.RevocarTodasDelUsuarioAsync(u.Id, Ahora);
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
    }
}
