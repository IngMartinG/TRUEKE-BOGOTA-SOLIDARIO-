using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IGeneradorToken
{
    /// <param name="conDosFactores">Agrega "amr":"mfa" (la sesión pasó la verificación en dos pasos).</param>
    (string Token, DateTime ExpiraUtc) Generar(Usuario usuario, bool conDosFactores = false);
}

/// <summary>
/// JWT HS256 mínimo (sub, role, sv = versión de seguridad, jti, iss, aud, iat/nbf/exp). Sin correo ni nombre: menos datos
/// personales en el token. La validación la hace el middleware JwtBearer de Presentacion con la misma clave.
/// </summary>
public sealed class GeneradorJwt : IGeneradorToken
{
    private static readonly byte[] CabeceraB64 = Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
    private readonly JwtOpciones _jwt;
    private readonly TimeProvider _reloj;

    public GeneradorJwt(IOptions<JwtOpciones> jwt, TimeProvider reloj)
    {
        _jwt = jwt.Value;
        _reloj = reloj;
    }

    public (string Token, DateTime ExpiraUtc) Generar(Usuario usuario, bool conDosFactores = false)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var expira = ahora.AddMinutes(_jwt.Minutos);
        var iat = new DateTimeOffset(ahora).ToUnixTimeSeconds();
        var carga = new Dictionary<string, object>
        {
            ["sub"] = usuario.Id.ToString(),
            ["role"] = usuario.Rol.ToString(),
            ["sv"] = usuario.VersionSeguridad,
            ["amr"] = conDosFactores ? "mfa" : "pwd", // método de autenticación (RFC 8176)
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iss"] = _jwt.Issuer,
            ["aud"] = _jwt.Audience,
            ["iat"] = iat,
            ["nbf"] = iat,
            ["exp"] = new DateTimeOffset(expira).ToUnixTimeSeconds()
        };
        var contenido = Base64Url(CabeceraB64) + "." + Base64Url(JsonSerializer.SerializeToUtf8Bytes(carga));
        var firma = HMACSHA256.HashData(Encoding.UTF8.GetBytes(_jwt.Key), Encoding.ASCII.GetBytes(contenido));
        return (contenido + "." + Base64Url(firma), expira);
    }

    private static string Base64Url(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
