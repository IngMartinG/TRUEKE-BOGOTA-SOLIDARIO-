using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Dinero real que ENTRA a la plataforma (recargas, suscripciones, beneficios). Nunca se mueve dinero entre usuarios.
/// Nace Pendiente (con los Eco-Puntos a canjear ya reservados) y solo pasa a Aprobado cuando la pasarela lo confirma.
/// </summary>
public class Pago
{
    private Pago() { Referencia = ""; } // EF Core

    public Pago(Guid usuarioId, ConceptoPago concepto, int montoCop, string referencia, int puntosCanjeados,
        Guid? publicacionId, string? documentoUrl, DateTime ahoraUtc)
    {
        if (montoCop <= 0) throw new ReglaDeNegocioException("El monto del pago debe ser mayor a cero.");
        if (puntosCanjeados < 0) throw new ReglaDeNegocioException("Los puntos canjeados no pueden ser negativos.");
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Concepto = concepto;
        MontoCop = montoCop;
        Referencia = referencia;
        PuntosCanjeados = puntosCanjeados;
        PublicacionId = publicacionId;
        DocumentoUrl = documentoUrl;
        FechaUtc = ahoraUtc;
        Estado = EstadoPago.Pendiente;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public ConceptoPago Concepto { get; private set; }
    public int MontoCop { get; private set; }
    public string Referencia { get; private set; }
    public int PuntosCanjeados { get; private set; }
    public Guid? PublicacionId { get; private set; }
    public string? DocumentoUrl { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public EstadoPago Estado { get; private set; }
    public string? ProveedorTransaccionId { get; private set; }
    public DateTime? FechaResolucionUtc { get; private set; }
    public string? NotaInterna { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public long MontoEnCentavos => MontoCop * 100L;

    public void Aprobar(string? transaccionId, DateTime ahoraUtc)
    {
        Exigir(EstadoPago.Pendiente);
        Estado = EstadoPago.Aprobado;
        ProveedorTransaccionId = transaccionId;
        FechaResolucionUtc = ahoraUtc;
    }

    public void Rechazar(string? transaccionId, DateTime ahoraUtc)
    {
        Exigir(EstadoPago.Pendiente);
        Estado = EstadoPago.Rechazado;
        ProveedorTransaccionId = transaccionId;
        FechaResolucionUtc = ahoraUtc;
    }

    public void Expirar(DateTime ahoraUtc)
    {
        Exigir(EstadoPago.Pendiente);
        Estado = EstadoPago.Expirado;
        FechaResolucionUtc = ahoraUtc;
    }

    /// <summary>Se cobró (o llegó una aprobación tardía) pero el beneficio no pudo aplicarse: requiere reembolso manual.</summary>
    public void MarcarRequiereRevision(string nota, string? transaccionId, DateTime ahoraUtc)
    {
        if (Estado is not (EstadoPago.Pendiente or EstadoPago.Expirado))
            throw new ReglaDeNegocioException("El pago ya fue resuelto.");
        Estado = EstadoPago.RequiereRevision;
        NotaInterna = nota.Length > 300 ? nota[..300] : nota;
        ProveedorTransaccionId = transaccionId;
        FechaResolucionUtc = ahoraUtc;
    }

    public void MarcarReembolsado(string? nota = null)
    {
        if (Estado is not (EstadoPago.Aprobado or EstadoPago.RequiereRevision))
            throw new ReglaDeNegocioException("Solo se puede reembolsar un pago aprobado o en revisión.");
        Estado = EstadoPago.Reembolsado;
        if (nota is not null) NotaInterna = nota.Length > 300 ? nota[..300] : nota;
    }

    private void Exigir(EstadoPago esperado)
    {
        if (Estado != esperado) throw new ReglaDeNegocioException("El pago ya fue resuelto.");
    }
}
