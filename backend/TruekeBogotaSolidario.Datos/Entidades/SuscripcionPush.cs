namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Un dispositivo (navegador) que aceptó recibir notificaciones push de la persona. El Endpoint lo entrega el servicio
/// de push del navegador (Google, Mozilla, Apple, Microsoft) y es único: si otra persona inicia sesión en ese mismo
/// navegador, la suscripción pasa a ser suya (nunca le llegan avisos de la cuenta anterior).
/// </summary>
public class SuscripcionPush
{
    public const int MaxPorUsuario = 10;

    private SuscripcionPush() { Endpoint = ""; P256dh = ""; Auth = ""; } // EF Core

    public SuscripcionPush(Guid usuarioId, string endpoint, string p256dh, string auth, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        Endpoint = endpoint;
        P256dh = p256dh;
        Auth = auth;
        UsuarioId = usuarioId;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Endpoint { get; private set; }
    /// <summary>Llave pública del navegador (cifrado de extremo a extremo del aviso).</summary>
    public string P256dh { get; private set; }
    public string Auth { get; private set; }
    /// <summary>Alta o última renovación.</summary>
    public DateTime FechaUtc { get; private set; }

    public void Renovar(Guid usuarioId, string p256dh, string auth, DateTime ahoraUtc)
    {
        UsuarioId = usuarioId;
        P256dh = p256dh;
        Auth = auth;
        FechaUtc = ahoraUtc;
    }
}
