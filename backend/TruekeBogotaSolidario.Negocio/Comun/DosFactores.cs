using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>TOTP (RFC 6238): HMAC-SHA1, pasos de 30 s, 6 dígitos — compatible con Google/Microsoft Authenticator, Authy, etc.</summary>
public static class Totp
{
    public const int SegundosPorPaso = 30;
    private const string AlfabetoBase32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] GenerarSecreto() => RandomNumberGenerator.GetBytes(20); // 160 bits, recomendado por RFC 4226

    public static long Paso(DateTimeOffset momento) => momento.ToUnixTimeSeconds() / SegundosPorPaso;

    public static string Calcular(byte[] secreto, long paso)
    {
        Span<byte> contador = stackalloc byte[8];
        for (var i = 7; i >= 0; i--) { contador[i] = (byte)(paso & 0xFF); paso >>= 8; }
        var hash = HMACSHA1.HashData(secreto, contador);
        var offset = hash[^1] & 0x0F;
        var binario = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binario % 1_000_000).ToString("D6");
    }

    /// <summary>Acepta el paso actual y ±1 (tolerancia de reloj del teléfono). Devuelve el paso coincidente o null. Comparación en tiempo constante.</summary>
    public static long? Verificar(byte[] secreto, string? codigo, DateTimeOffset ahora)
    {
        codigo = (codigo ?? "").Replace(" ", "");
        if (codigo.Length != 6 || !codigo.All(char.IsAsciiDigit)) return null;
        var actual = Paso(ahora);
        for (var delta = -1; delta <= 1; delta++)
        {
            var esperado = Calcular(secreto, actual + delta);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(esperado), Encoding.ASCII.GetBytes(codigo)))
                return actual + delta;
        }
        return null;
    }

    public static string Base32(byte[] datos)
    {
        var sb = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in datos)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5) { sb.Append(AlfabetoBase32[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(AlfabetoBase32[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    /// <summary>URI que el front convierte en código QR para escanear con la app autenticadora.</summary>
    public static string UriOtpauth(string emisor, string cuenta, string secretoBase32)
        => $"otpauth://totp/{Uri.EscapeDataString(emisor)}:{Uri.EscapeDataString(cuenta)}?secret={secretoBase32}&issuer={Uri.EscapeDataString(emisor)}&algorithm=SHA1&digits=6&period={SegundosPorPaso}";

    /// <summary>10 códigos de recuperación "XXXX-XXXX" (sin caracteres ambiguos).</summary>
    public static IReadOnlyList<string> GenerarCodigosRecuperacion(int cantidad = 10)
    {
        const string alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        return Enumerable.Range(0, cantidad).Select(_ =>
        {
            var c = new char[9];
            for (var i = 0; i < 9; i++) c[i] = i == 4 ? '-' : alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
            return new string(c);
        }).ToList();
    }

    public static string HashCodigoRecuperacion(string codigo)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((codigo ?? "").Trim().ToUpperInvariant().Replace(" ", "")))).ToLowerInvariant();
}

/// <summary>
/// Cifrado AES-256-GCM para secretos que deben recuperarse (el de la app autenticadora). La llave
/// (Seguridad:ClaveCifrado, 32 bytes en base64) vive fuera de la base de datos: una copia de la BD no expone los secretos.
/// </summary>
public interface ICifradorSecretos
{
    string Cifrar(byte[] datos);
    byte[] Descifrar(string cifrado);
}

public sealed class CifradorAesGcm : ICifradorSecretos
{
    private readonly byte[] _llave;

    public CifradorAesGcm(IOptions<SeguridadOpciones> opciones)
    {
        var valor = opciones.Value.ClaveCifrado;
        byte[]? llave = null;
        try { llave = string.IsNullOrWhiteSpace(valor) ? null : Convert.FromBase64String(valor); }
        catch (FormatException) { }
        if (llave is not { Length: 32 })
            throw new InvalidOperationException("Seguridad:ClaveCifrado debe ser 32 bytes en base64 (genera una con: openssl rand -base64 32).");
        _llave = llave;
    }

    public string Cifrar(byte[] datos)
    {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var cifrado = new byte[datos.Length];
        using var aes = new AesGcm(_llave, tag.Length);
        aes.Encrypt(nonce, datos, cifrado, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cifrado).ToArray());
    }

    public byte[] Descifrar(string cifrado)
    {
        var todo = Convert.FromBase64String(cifrado);
        var n = AesGcm.NonceByteSizes.MaxSize;
        var t = AesGcm.TagByteSizes.MaxSize;
        var datos = new byte[todo.Length - n - t];
        using var aes = new AesGcm(_llave, t);
        aes.Decrypt(todo.AsSpan(0, n), todo.AsSpan(n + t), todo.AsSpan(n, t), datos); // falla si alguien alteró el valor
        return datos;
    }
}
