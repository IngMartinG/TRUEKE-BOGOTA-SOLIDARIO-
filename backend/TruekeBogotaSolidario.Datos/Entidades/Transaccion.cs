namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Registro inmutable de un intercambio completado (auditoría + control anti-farmeo).</summary>
public class Transaccion
{
    private Transaccion() { } // EF Core

    public Transaccion(Guid publicacionId, Guid solicitudId, Guid oferenteId, Guid receptorId, ModoTransaccion modo,
        bool puntosOferente, bool puntosReceptor, DateTime? fechaUtc = null)
    {
        Id = Guid.NewGuid();
        PublicacionId = publicacionId;
        SolicitudId = solicitudId;
        OferenteId = oferenteId;
        ReceptorId = receptorId;
        Modo = modo;
        FechaUtc = fechaUtc ?? DateTime.UtcNow;
        PuntosOtorgadosOferente = puntosOferente;
        PuntosOtorgadosReceptor = puntosReceptor;
    }

    public Guid Id { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Guid SolicitudId { get; private set; }
    public Guid OferenteId { get; private set; }
    public Guid ReceptorId { get; private set; }
    public ModoTransaccion Modo { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public bool PuntosOtorgadosOferente { get; private set; }
    public bool PuntosOtorgadosReceptor { get; private set; }
}
