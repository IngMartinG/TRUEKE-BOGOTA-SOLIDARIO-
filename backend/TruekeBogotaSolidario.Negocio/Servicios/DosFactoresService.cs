using Microsoft.Extensions.Logging;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Comprueba un código TOTP (sin repetir pasos) o consume un código de recuperación. Marca cambios; quien llama guarda.</summary>
public sealed class VerificadorDosFactores
{
    private readonly ICifradorSecretos _cifrador;
    private readonly TimeProvider _reloj;
    public VerificadorDosFactores(ICifradorSecretos cifrador, TimeProvider reloj) { _cifrador = cifrador; _reloj = reloj; }

    public bool Verificar(Usuario u, string? codigo)
    {
        if (!u.DosFactoresActivo || u.SecretoDosFactoresCifrado is null || string.IsNullOrWhiteSpace(codigo)) return false;
        var paso = Totp.Verificar(_cifrador.Descifrar(u.SecretoDosFactoresCifrado), codigo, _reloj.GetUtcNow());
        if (paso.HasValue) return u.RegistrarPasoDosFactores(paso.Value); // false si ese código ya se usó
        return u.UsarCodigoRecuperacion(Totp.HashCodigoRecuperacion(codigo));
    }
}

public interface IDosFactoresService
{
    /// <summary>Genera un secreto nuevo (aún inactivo) y devuelve la URI otpauth para el código QR.</summary>
    Task<ConfiguracionDosFactoresDto> IniciarAsync(Guid actorId);
    /// <summary>Activa con el primer código de la app. Cierra las demás sesiones y devuelve una sesión nueva con 2FA + códigos de recuperación.</summary>
    Task<(ResultadoAutenticacion Sesion, IReadOnlyList<string> CodigosRecuperacion)> ActivarAsync(Guid actorId, string codigo);
    Task<ResultadoAutenticacion> DesactivarAsync(Guid actorId, string codigo);
    Task<IReadOnlyList<string>> RegenerarCodigosAsync(Guid actorId, string codigo);
}

public sealed class DosFactoresService : IDosFactoresService
{
    private const string Emisor = "Trueke Bogotá Solidario";
    private const string CodigoInvalido = "El código no es válido o ya fue usado. Revisa la hora de tu teléfono e inténtalo de nuevo.";

    private readonly IUsuarioRepository _usuarios;
    private readonly ISesionRefreshRepository _refrescos;
    private readonly IUnidadDeTrabajo _uow;
    private readonly ICifradorSecretos _cifrador;
    private readonly VerificadorDosFactores _verificador;
    private readonly EmisorSesiones _emisor;
    private readonly ISesionService _sesiones;
    private readonly TimeProvider _reloj;
    private readonly ILogger<DosFactoresService> _log;

    public DosFactoresService(IUsuarioRepository usuarios, ISesionRefreshRepository refrescos, IUnidadDeTrabajo uow, ICifradorSecretos cifrador,
        VerificadorDosFactores verificador, EmisorSesiones emisor, ISesionService sesiones, TimeProvider reloj, ILogger<DosFactoresService> log)
    {
        _usuarios = usuarios; _refrescos = refrescos; _uow = uow; _cifrador = cifrador; _verificador = verificador; _emisor = emisor;
        _sesiones = sesiones; _reloj = reloj; _log = log;
    }

    private async Task<Usuario> CargarAsync(Guid id) => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    public async Task<ConfiguracionDosFactoresDto> IniciarAsync(Guid actorId)
    {
        var u = await CargarAsync(actorId);
        var secreto = Totp.GenerarSecreto();
        u.PrepararDosFactores(_cifrador.Cifrar(secreto));
        await _uow.GuardarCambiosAsync();
        var base32 = Totp.Base32(secreto);
        return new ConfiguracionDosFactoresDto(base32, Totp.UriOtpauth(Emisor, u.Correo, base32));
    }

    public async Task<(ResultadoAutenticacion, IReadOnlyList<string>)> ActivarAsync(Guid actorId, string codigo)
    {
        var u = await CargarAsync(actorId);
        if (u.DosFactoresActivo) throw new ReglaDeNegocioException("La verificación en dos pasos ya está activa.");
        if (u.SecretoDosFactoresCifrado is null) throw new ReglaDeNegocioException("Primero genera el código QR.");
        var paso = Totp.Verificar(_cifrador.Descifrar(u.SecretoDosFactoresCifrado), codigo, _reloj.GetUtcNow())
                   ?? throw new ReglaDeNegocioException(CodigoInvalido);

        var codigos = Totp.GenerarCodigosRecuperacion();
        u.ActivarDosFactores(codigos.Select(Totp.HashCodigoRecuperacion), paso); // sube VersionSeguridad
        await _refrescos.RevocarTodasDelUsuarioAsync(u.Id, _reloj.GetUtcNow().UtcDateTime);
        var sesion = _emisor.Emitir(u, conDosFactores: true);
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
        _log.LogInformation("2FA activado {UsuarioId}", u.Id);
        return (sesion, codigos);
    }

    public async Task<ResultadoAutenticacion> DesactivarAsync(Guid actorId, string codigo)
    {
        var u = await CargarAsync(actorId);
        if (!u.DosFactoresActivo) throw new ReglaDeNegocioException("La verificación en dos pasos no está activa.");
        if (!_verificador.Verificar(u, codigo)) throw new ReglaDeNegocioException(CodigoInvalido);
        u.DesactivarDosFactores(); // sube VersionSeguridad
        await _refrescos.RevocarTodasDelUsuarioAsync(u.Id, _reloj.GetUtcNow().UtcDateTime);
        var sesion = _emisor.Emitir(u);
        await _uow.GuardarCambiosAsync();
        _sesiones.Invalidar(u.Id);
        _log.LogWarning("2FA desactivado {UsuarioId}", u.Id);
        return sesion;
    }

    public async Task<IReadOnlyList<string>> RegenerarCodigosAsync(Guid actorId, string codigo)
    {
        var u = await CargarAsync(actorId);
        if (!u.DosFactoresActivo) throw new ReglaDeNegocioException("La verificación en dos pasos no está activa.");
        var paso = u.SecretoDosFactoresCifrado is null ? null : Totp.Verificar(_cifrador.Descifrar(u.SecretoDosFactoresCifrado), codigo, _reloj.GetUtcNow());
        if (paso is null || !u.RegistrarPasoDosFactores(paso.Value)) throw new ReglaDeNegocioException(CodigoInvalido); // solo con la app
        var codigos = Totp.GenerarCodigosRecuperacion();
        u.ReemplazarCodigosRecuperacion(codigos.Select(Totp.HashCodigoRecuperacion));
        await _uow.GuardarCambiosAsync();
        return codigos;
    }
}
