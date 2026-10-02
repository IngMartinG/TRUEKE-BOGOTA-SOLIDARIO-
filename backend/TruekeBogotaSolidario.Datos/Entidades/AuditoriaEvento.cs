namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Bitácora inmutable de acciones sensibles (cambios de rol, moderación, verificaciones). Nunca guarda datos personales sensibles.</summary>
public class AuditoriaEvento
{
    private AuditoriaEvento() { Accion = ""; ObjetivoTipo = ""; } // EF Core

    public AuditoriaEvento(Guid actorId, string accion, string objetivoTipo, Guid objetivoId, string? detalle, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        ActorId = actorId;
        Accion = accion;
        ObjetivoTipo = objetivoTipo;
        ObjetivoId = objetivoId;
        Detalle = detalle is { Length: > 300 } ? detalle[..300] : detalle;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public Guid ActorId { get; private set; }
    public string Accion { get; private set; }
    public string ObjetivoTipo { get; private set; }
    public Guid ObjetivoId { get; private set; }
    public string? Detalle { get; private set; }
}
