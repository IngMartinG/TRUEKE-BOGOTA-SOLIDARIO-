namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Registro inmutable de cada cambio del saldo de Eco-Puntos (historial que ve la persona y auditoría).
/// Lo crea Usuario al acreditar o debitar: nunca se edita ni se borra.
/// </summary>
public class MovimientoEcoPuntos
{
    private MovimientoEcoPuntos() { } // EF Core

    internal MovimientoEcoPuntos(Guid usuarioId, int puntos, ConceptoEcoPuntos concepto, Guid? referenciaId, string? detalle, int saldoResultante)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Puntos = puntos;
        Concepto = concepto;
        ReferenciaId = referenciaId;
        Detalle = detalle is null ? null : (detalle.Length <= 160 ? detalle : detalle[..160]);
        SaldoResultante = saldoResultante;
        FechaUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    /// <summary>Positivo si se ganaron o devolvieron puntos; negativo si se gastaron o retiraron.</summary>
    public int Puntos { get; private set; }
    public ConceptoEcoPuntos Concepto { get; private set; }
    /// <summary>Lo que originó el movimiento: solicitud (intercambio), pago (recarga, canje) o publicación (impulsar).</summary>
    public Guid? ReferenciaId { get; private set; }
    /// <summary>Texto corto para la persona, p. ej. el título de la publicación.</summary>
    public string? Detalle { get; private set; }
    public int SaldoResultante { get; private set; }
    public DateTime FechaUtc { get; private set; }
}
