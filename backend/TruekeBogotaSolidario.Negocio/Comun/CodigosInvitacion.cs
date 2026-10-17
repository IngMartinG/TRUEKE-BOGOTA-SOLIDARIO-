using System.Security.Cryptography;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Códigos de invitación cortos y fáciles de dictar: sin 0/O, 1/I/L ni otros caracteres que se confunden.</summary>
public static class CodigosInvitacion
{
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Largo = 8;

    public static string Generar() => RandomNumberGenerator.GetString(Alfabeto, Largo);

    /// <summary>Lo escrito a mano o pegado del enlace: sin espacios y en mayúsculas.</summary>
    public static string Normalizar(string codigo) => codigo.Trim().ToUpperInvariant();
}
