using System.Security.Cryptography;

namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Hash de contraseñas con PBKDF2-HMAC-SHA256 (sin paquetes externos).
/// Formato almacenado: v1.{iteraciones}.{salt b64}.{hash b64}. Las iteraciones viajan dentro del hash,
/// por lo que se pueden subir en el futuro sin invalidar contraseñas existentes.
/// </summary>
public static class PasswordHasher
{
    private const int Iteraciones = 600_000; // recomendación OWASP para PBKDF2-SHA256
    private const int BytesSal = 16;
    private const int BytesHash = 32;
    private const int IteracionesMaximasAceptadas = 5_000_000; // evita DoS con un hash manipulado

    // Hash "señuelo": permite gastar el mismo tiempo cuando el correo no existe (evita enumeración por tiempos).
    private static readonly Lazy<string> HashSenuelo = new(() => Hash("Senuelo-Trueke-2026"));

    public static string Hash(string claveEnClaro)
    {
        var sal = RandomNumberGenerator.GetBytes(BytesSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(claveEnClaro, sal, Iteraciones, HashAlgorithmName.SHA256, BytesHash);
        return $"v1.{Iteraciones}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verificar(string claveEnClaro, string almacenado)
    {
        var partes = almacenado.Split('.');
        if (partes.Length != 4 || partes[0] != "v1") return false;
        if (!int.TryParse(partes[1], out var iteraciones) || iteraciones <= 0 || iteraciones > IteracionesMaximasAceptadas) return false;
        try
        {
            var sal = Convert.FromBase64String(partes[2]);
            var esperado = Convert.FromBase64String(partes[3]);
            var calculado = Rfc2898DeriveBytes.Pbkdf2(claveEnClaro, sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Consume el mismo tiempo que una verificación real (para correos inexistentes).</summary>
    public static void VerificarSenuelo(string claveEnClaro) => Verificar(claveEnClaro, HashSenuelo.Value);
}
