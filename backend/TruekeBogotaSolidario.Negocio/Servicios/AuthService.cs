using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IAuthService
{
    Task<ResultadoAutenticacion> RegistrarAsync(RegistroRequest r);
    Task<ResultadoAutenticacion> LoginAsync(LoginRequest r);
    /// <summary>Inicia sesión (o crea la cuenta) con un ID token de Google validado en el servidor. Emite NUESTRO JWT.</summary>
    Task<ResultadoAutenticacion> LoginGoogleAsync(GoogleLoginRequest r);
    /// <summary>Rota el token de refresco. Reusar uno ya reemplazado revoca toda la familia (posible robo).</summary>
    Task<ResultadoAutenticacion> RefrescarAsync(string? tokenRefresco);
    /// <summary>Cierra la sesión de ESTE dispositivo (revoca la familia del token de refresco). Nunca falla.</summary>
    Task CerrarSesionAsync(string? tokenRefresco);
    Task<UsuarioDto> ObtenerPerfilAsync(Guid actorId);
    Task<UsuarioDto> ActualizarPerfilAsync(Guid actorId, ActualizarPerfilRequest r);
    /// <summary>Nombre comercial y NIT (solo con plan Empresa vigente).</summary>
    Task<UsuarioDto> ActualizarPerfilEmpresaAsync(Guid actorId, PerfilEmpresaRequest r);
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
    private readonly CorreosCuenta _correos;
    private readonly ICorreoSaliente _salida;
    private readonly IValidadorGoogle _google;
    private readonly IVerificadorCaptcha _captcha;
    private readonly VerificadorDosFactores _dosFactores;
    private readonly TimeProvider _reloj;
    private readonly ILogger<AuthService> _log;

    public AuthService(IUsuarioRepository usuarios, ISesionRefreshRepository refrescos, IUnidadDeTrabajo uow, EmisorSesiones emisor,
        ISesionService sesiones, IOptions<SeguridadOpciones> seg, IOptions<LegalOpciones> legal, CorreosCuenta correos,
        ICorreoSaliente salida, IValidadorGoogle google, IVerificadorCaptcha captcha, VerificadorDosFactores dosFactores,
        TimeProvider reloj, ILogger<AuthService> log, IImportadorFotoPerfil importadorFoto)
    {
        _usuarios = usuarios; _refrescos = refrescos; _uow = uow; _emisor = emisor; _sesiones = sesiones; _seg = seg.Value;
        _legal = legal.Value; _correos = correos; _salida = salida; _google = google; _captcha = captcha; _dosFactores = dosFactores;
        _reloj = reloj; _log = log; _importadorFoto = importadorFoto;
    }

    private readonly IImportadorFotoPerfil _importadorFoto;

    private DateTime Ahora => _reloj.GetUtcNow().UtcDateTime;

    public async Task<ResultadoAutenticacion> RegistrarAsync(RegistroRequest r)
    {
        await _captcha.ExigirAsync(r.CaptchaToken, AccionesCaptcha.Registro);
        var correo = Usuario.NormalizarCorreo(r.Correo);
        if (CorreosDesechables.EsDesechable(correo, _seg.DominiosCorreoBloqueados))
            throw new ReglaDeNegocioException("No aceptamos correos temporales o desechables. Usa tu correo personal.");
        // Alias del mismo buzón (yo+1@gmail.com, y.o@gmail.com) cuentan como el mismo correo: una persona, una cuenta.
        if (await _usuarios.ExisteCorreoCanonicoAsync(Usuario.CanonizarCorreo(correo)))
            throw new ReglaDeNegocioException("Ya existe una cuenta con ese correo.");

        // Los Eco-Puntos de bienvenida se entregan al confirmar el correo (no al registrarse).
        var usuario = new Usuario(r.NombreCompleto, r.Localidad, correo, r.Clave, r.MunicipioCodigo);
        usuario.AceptarPoliticaDatos(_legal.VersionPoliticaDatos, Ahora);
        _usuarios.Agregar(usuario);
        var sesion = _emisor.Emitir(usuario);
        var confirmacion = await _correos.PrepararVerificacionAsync(usuario);
        await _uow.GuardarCambiosAsync(); // el índice único del correo cubre el registro simultáneo
        _salida.Encolar(confirmacion);    // solo después de guardar: el enlace siempre corresponde a un token persistido
        _log.LogInformation("Usuario registrado {UsuarioId}", usuario.Id);
        return sesion;
    }

    public async Task<ResultadoAutenticacion> LoginAsync(LoginRequest r)
    {
        await _captcha.ExigirAsync(r.CaptchaToken, AccionesCaptcha.Login);
        var ahora = Ahora;
        var usuario = await _usuarios.ObtenerPorCorreoCanonicoAsync(Usuario.CanonizarCorreo(r.Correo));

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

        ExigirNoSuspendido(usuario, ahora); // tras validar la clave: a un tercero no se le revela nada
        var conDosFactores = await ExigirSegundoFactorAsync(usuario, r.CodigoDosFactores, ahora);
        usuario.RegistrarLoginExitoso();
        var sesion = _emisor.Emitir(usuario, conDosFactores: conDosFactores);
        await _uow.GuardarCambiosAsync();
        return sesion;
    }

    /// <summary>
    /// Si la cuenta tiene 2FA: sin código → 401 "2fa_requerido" (el front muestra el campo); código incorrecto → cuenta como
    /// intento fallido (mismo bloqueo que la clave: 5 intentos), así no se puede adivinar el código de 6 dígitos.
    /// </summary>
    private async Task<bool> ExigirSegundoFactorAsync(Usuario usuario, string? codigo, DateTime ahora)
    {
        if (!usuario.DosFactoresActivo) return false;
        if (string.IsNullOrWhiteSpace(codigo)) throw new DosFactoresRequeridoException();
        if (_dosFactores.Verificar(usuario, codigo)) return true;
        usuario.RegistrarLoginFallido(_seg.MaxIntentosLogin, TimeSpan.FromMinutes(_seg.MinutosBloqueo), ahora);
        try { await _uow.GuardarCambiosAsync(); }
        catch (ConflictoDeConcurrenciaException) { /* otro intento simultáneo ya actualizó el contador */ }
        throw new AutenticacionException("El código de verificación no es correcto o ya fue usado.");
    }

    private static void ExigirNoSuspendido(Usuario u, DateTime ahora)
    {
        if (!u.SuspensionVigente(ahora)) return;
        var hasta = u.SuspendidoHasta is { } h ? $" hasta el {h:yyyy-MM-dd}" : "";
        throw new AccesoDenegadoException($"Tu cuenta está suspendida{hasta}. Motivo: {u.MotivoSuspension}");
    }

    public async Task<ResultadoAutenticacion> LoginGoogleAsync(GoogleLoginRequest r)
    {
        if (!_google.Habilitado) throw new ReglaDeNegocioException("El inicio de sesión con Google no está habilitado.");
        var id = await _google.ValidarAsync(r.IdToken) ?? throw new AutenticacionException("No se pudo validar tu cuenta de Google.");
        if (!id.CorreoVerificado) throw new AutenticacionException("Tu cuenta de Google no tiene el correo verificado.");

        var ahora = Ahora;
        var creada = false;
        var usuario = await _usuarios.ObtenerPorGoogleSubAsync(id.Sub);
        if (usuario is null)
        {
            usuario = await _usuarios.ObtenerPorCorreoCanonicoAsync(Usuario.CanonizarCorreo(id.Correo));
            if (usuario is not null)
            {
                // Vincula una cuenta existente con el mismo correo (Google ya demostró que el correo es de esta persona).
                if (usuario.GoogleSub is not null) throw new AutenticacionException("No se pudo validar tu cuenta de Google.");
                if (!usuario.CorreoVerificado)
                {
                    // La cuenta se creó con clave pero nadie confirmó el correo: podría haberla creado un tercero
                    // para esperar a la víctima. Se elimina esa clave y se cierran sus sesiones.
                    usuario.QuitarClave();
                    await _refrescos.RevocarTodasDelUsuarioAsync(usuario.Id, ahora);
                    _log.LogWarning("Clave eliminada al vincular Google a una cuenta no verificada {UsuarioId}", usuario.Id);
                }
                usuario.VincularGoogle(id.Sub);
                usuario.MarcarCorreoVerificado(ahora);
            }
            else
            {
                if (!r.AceptoPoliticaDatos)
                    throw new ReglaDeNegocioException("Para crear tu cuenta debes aceptar la política de tratamiento de datos personales.");
                usuario = Usuario.CrearDesdeGoogle(id.Nombre ?? "", id.Correo, id.Sub, ahora); // correo verificado por Google: recibe el bono
                usuario.AceptarPoliticaDatos(_legal.VersionPoliticaDatos, ahora);
                _usuarios.Agregar(usuario);
                creada = true;
            }
        }
        if (usuario.EstaEliminado) throw new AutenticacionException("No se pudo validar tu cuenta de Google.");
        ExigirNoSuspendido(usuario, ahora);
        var conDosFactores = await ExigirSegundoFactorAsync(usuario, r.CodigoDosFactores, ahora);
        // Sin foto propia, se trae la de Google (copiada y limpia). Si falla, la persona la sube a mano: el login sigue.
        if (string.IsNullOrWhiteSpace(usuario.FotoUrl) && await _importadorFoto.ImportarAsync(usuario.Id, id.Foto) is { } foto)
            usuario.CambiarFoto(foto);

        usuario.RegistrarLoginExitoso();
        var sesion = _emisor.Emitir(usuario, conDosFactores: conDosFactores);
        await _uow.GuardarCambiosAsync(); // índices únicos de correo y GoogleSub cubren dos altas simultáneas
        _sesiones.Invalidar(usuario.Id);
        if (creada) _log.LogInformation("Usuario registrado con Google {UsuarioId}", usuario.Id);
        return sesion with { CuentaCreada = creada };
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
        if (usuario is null || usuario.EstaEliminado || usuario.SuspensionVigente(ahora)) throw new AutenticacionException(expirada);
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
        u.ActualizarPerfil(r.NombreCompleto, r.Localidad, r.MunicipioCodigo);
        await _uow.GuardarCambiosAsync();
        return Mapeos.AUsuarioDto(u, Ahora);
    }

    public async Task<UsuarioDto> ActualizarPerfilEmpresaAsync(Guid actorId, PerfilEmpresaRequest r)
    {
        var u = await CargarAsync(actorId);
        u.ActualizarPerfilEmpresa(r.NombreComercial, r.Nit, Ahora);
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
