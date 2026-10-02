using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Tokens opacos de un solo propósito (refresco, verificación de correo, restablecimiento). Solo su hash se persiste.</summary>
public static class TokensSeguros
{
    /// <summary>256 bits aleatorios en base64url (43 caracteres).</summary>
    public static string Generar()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Descarta valores absurdos antes de tocar la base de datos.</summary>
    public static bool FormatoValido(string? token)
        => token is { Length: >= 20 and <= 100 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}

/// <summary>
/// Emite una sesión completa: JWT de acceso corto + token de refresco rotativo (que Presentacion guarda en una cookie HttpOnly).
/// Solo agrega entidades: quien la usa confirma con IUnidadDeTrabajo.
/// </summary>
public sealed class EmisorSesiones
{
    private readonly IGeneradorToken _tokens;
    private readonly ISesionRefreshRepository _refrescos;
    private readonly SeguridadOpciones _seg;
    private readonly TimeProvider _reloj;

    public EmisorSesiones(IGeneradorToken tokens, ISesionRefreshRepository refrescos, IOptions<SeguridadOpciones> seg, TimeProvider reloj)
    {
        _tokens = tokens; _refrescos = refrescos; _seg = seg.Value; _reloj = reloj;
    }

    /// <param name="anterior">Si se está rotando, el token usado (queda reemplazado; se conservan la familia y la marca de 2FA).</param>
    /// <param name="conDosFactores">El usuario acaba de demostrar el segundo factor (solo aplica a sesiones nuevas).</param>
    public ResultadoAutenticacion Emitir(Usuario usuario, SesionRefresh? anterior = null, bool conDosFactores = false)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var mfa = anterior?.ConDosFactores ?? conDosFactores;
        var token = TokensSeguros.Generar();
        var nueva = new SesionRefresh(usuario.Id, anterior?.FamiliaId ?? Guid.NewGuid(), TokensSeguros.Hash(token), ahora,
            ahora.AddDays(_seg.DiasRefresco), anterior?.ExpiraFamiliaUtc ?? ahora.AddDays(_seg.DiasMaximosSesion), mfa);
        _refrescos.Agregar(nueva);
        anterior?.MarcarReemplazada(nueva.Id, ahora);

        var (jwt, expira) = _tokens.Generar(usuario, mfa);
        return new ResultadoAutenticacion(new SesionDto(jwt, expira, Mapeos.AUsuarioDto(usuario, ahora)), token, nueva.ExpiraUtc);
    }
}
