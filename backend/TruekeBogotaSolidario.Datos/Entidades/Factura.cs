using System.Diagnostics.CodeAnalysis;
using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Soporte de la factura electrónica de un pago aprobado. Nace "Pendiente" en la MISMA transacción que aprueba el pago
/// (nunca se pierde una venta sin facturar) y pasa a "Emitida" cuando se obtiene el número y el CUFE de la DIAN.
/// Los precios de la plataforma incluyen IVA: aquí se discrimina base e impuesto. Los datos del comprador se copian
/// (la factura no cambia aunque el usuario edite o elimine su cuenta).
/// </summary>
public class Factura
{
    /// <summary>Identificación genérica de "consumidor final" aceptada por la DIAN cuando el comprador no da sus datos.</summary>
    public const string DocumentoConsumidorFinal = "222222222222";

    private Factura() { Referencia = ""; Descripcion = ""; CompradorDocumento = ""; CompradorNombre = ""; CompradorCorreo = ""; } // EF Core

    public Factura(Pago pago, Usuario comprador, string descripcion, decimal ivaPorcentaje, DateTime ahoraUtc)
    {
        if (pago.Estado != EstadoPago.Aprobado) throw new ReglaDeNegocioException("Solo se factura un pago aprobado.");
        if (ivaPorcentaje is < 0 or > 100) throw new ReglaDeNegocioException("Porcentaje de IVA no válido.");
        Id = Guid.NewGuid();
        PagoId = pago.Id;
        Referencia = pago.Referencia;
        UsuarioId = comprador.Id;
        Concepto = pago.Concepto;
        Descripcion = descripcion.Length > 200 ? descripcion[..200] : descripcion;
        FechaUtc = ahoraUtc;
        TotalCop = pago.MontoCop;
        IvaPorcentaje = ivaPorcentaje;
        BaseCop = Math.Round(pago.MontoCop / (1 + ivaPorcentaje / 100m), 2, MidpointRounding.AwayFromZero);
        IvaCop = pago.MontoCop - BaseCop;

        // Lo que la persona eligió al pagar manda (con la copia de sus datos de ese momento). Los pagos anteriores a la
        // elección usan los datos completos del perfil, si los hay.
        ElegidaANombre = pago.FacturaANombre;
        FechaEleccionUtc = pago.FechaEleccionFacturaUtc;
        var datos = pago.FacturaANombre switch
        {
            true => pago.DatosCompradorElegidos,
            false => null,
            null => comprador.DatosFacturacion
        };
        AsignarComprador(datos, comprador.Correo);
        Estado = EstadoFactura.Pendiente;
    }

    /// <summary>Copia de una factura emitida con el comprador corregido (la original queda para nota crédito).</summary>
    private Factura(Factura original, DatosComprador comprador, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        PagoId = original.PagoId;
        Referencia = original.Referencia;
        UsuarioId = original.UsuarioId;
        Concepto = original.Concepto;
        Descripcion = original.Descripcion;
        FechaUtc = ahoraUtc;
        TotalCop = original.TotalCop;
        IvaPorcentaje = original.IvaPorcentaje;
        BaseCop = original.BaseCop;
        IvaCop = original.IvaCop;
        ElegidaANombre = original.ElegidaANombre;
        FechaEleccionUtc = original.FechaEleccionUtc;
        ReemplazaAId = original.Id;
        AsignarComprador(comprador, original.CompradorCorreo);
        Estado = EstadoFactura.Pendiente;
    }

    [MemberNotNull(nameof(CompradorDocumento), nameof(CompradorNombre), nameof(CompradorCorreo))]
    private void AsignarComprador(DatosComprador? d, string correoPorDefecto)
    {
        if (d is not null)
        {
            CompradorTipoDocumento = d.TipoDocumento;
            CompradorDocumento = d.Documento;
            CompradorNombre = d.Nombre;
            CompradorCorreo = d.Correo;
            CompradorDireccion = d.Direccion;
            CompradorMunicipioCodigo = d.MunicipioCodigo;
        }
        else
        {
            CompradorTipoDocumento = null;
            CompradorDocumento = DocumentoConsumidorFinal;
            CompradorNombre = "Consumidor final";
            CompradorCorreo = correoPorDefecto;
            CompradorDireccion = null;
            CompradorMunicipioCodigo = null;
        }
    }

    /// <summary>Lo que eligió la persona al pagar: true = a su nombre; false = consumidor final; null = pago anterior a la regla.</summary>
    public bool? ElegidaANombre { get; private set; }
    public DateTime? FechaEleccionUtc { get; private set; }
    /// <summary>Esta factura reemplaza a otra ya emitida cuyo comprador se corrigió.</summary>
    public Guid? ReemplazaAId { get; private set; }
    public Guid? ReemplazadaPorId { get; private set; }
    /// <summary>Auditoría de la última corrección del comprador (solo SuperUsuario).</summary>
    public Guid? CorregidaPorId { get; private set; }
    public DateTime? FechaCorreccionUtc { get; private set; }
    public string? MotivoCorreccion { get; private set; }

    /// <summary>
    /// SuperUsuario: la persona necesita la factura a su nombre (se equivocó al elegir o hubo un error). Si aún no se emitió,
    /// se corrige aquí. Para una ya emitida usa <see cref="Reemplazar"/>.
    /// </summary>
    public void CorregirComprador(DatosComprador comprador, Guid superUsuarioId, string motivo, DateTime ahoraUtc)
    {
        if (Estado != EstadoFactura.Pendiente) throw new ReglaDeNegocioException("Solo se corrige directamente una factura pendiente; si ya se emitió, reemplázala.");
        RegistrarCorreccion(superUsuarioId, motivo, ahoraUtc);
        AsignarComprador(comprador, CompradorCorreo);
    }

    /// <summary>
    /// SuperUsuario, factura YA emitida: esta queda "Reemplazada" y marcada para nota crédito ante la DIAN, y se devuelve una
    /// nueva factura pendiente con el comprador corregido (mismo pago y mismos valores).
    /// </summary>
    public Factura Reemplazar(DatosComprador comprador, Guid superUsuarioId, string motivo, DateTime ahoraUtc)
    {
        if (Estado != EstadoFactura.Emitida) throw new ReglaDeNegocioException("Solo se reemplaza una factura emitida; si está pendiente, corrígela.");
        RegistrarCorreccion(superUsuarioId, motivo, ahoraUtc);
        var nueva = new Factura(this, comprador, ahoraUtc);
        nueva.RegistrarCorreccion(superUsuarioId, motivo, ahoraUtc);
        Estado = EstadoFactura.Reemplazada;
        RequiereNotaCredito = true;
        ReemplazadaPorId = nueva.Id;
        return nueva;
    }

    private void RegistrarCorreccion(Guid superUsuarioId, string motivo, DateTime ahoraUtc)
    {
        var m = (motivo ?? "").Trim();
        if (m.Length is < 10 or > 300 || m.Any(char.IsControl))
            throw new ReglaDeNegocioException("Explica el motivo de la corrección (entre 10 y 300 caracteres): queda en la auditoría.");
        CorregidaPorId = superUsuarioId;
        FechaCorreccionUtc = ahoraUtc;
        MotivoCorreccion = m;
    }

    public Guid Id { get; private set; }
    public Guid PagoId { get; private set; }
    /// <summary>Referencia del pago (TRK-…): enlaza la factura con la transacción de Wompi.</summary>
    public string Referencia { get; private set; }
    public Guid UsuarioId { get; private set; }
    public ConceptoPago Concepto { get; private set; }
    public string Descripcion { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public int TotalCop { get; private set; }
    public decimal IvaPorcentaje { get; private set; }
    public decimal BaseCop { get; private set; }
    public decimal IvaCop { get; private set; }

    public TipoDocumentoFiscal? CompradorTipoDocumento { get; private set; }
    public string CompradorDocumento { get; private set; }
    public string CompradorNombre { get; private set; }
    public string CompradorCorreo { get; private set; }
    public string? CompradorDireccion { get; private set; }
    public string? CompradorMunicipioCodigo { get; private set; }

    public EstadoFactura Estado { get; private set; }
    /// <summary>Prefijo y consecutivo autorizados por la DIAN (p. ej. "FE-1024").</summary>
    public string? NumeroDian { get; private set; }
    /// <summary>Código Único de Factura Electrónica.</summary>
    public string? Cufe { get; private set; }
    public DateTime? FechaEmisionUtc { get; private set; }
    /// <summary>El pago se reembolsó después de emitir la factura: hay que emitir una nota crédito.</summary>
    public bool RequiereNotaCredito { get; private set; }
    public string? NotaInterna { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void MarcarEmitida(string numeroDian, string cufe, DateTime ahoraUtc)
    {
        if (Estado != EstadoFactura.Pendiente) throw new ReglaDeNegocioException("La factura ya fue emitida o anulada.");
        var numero = (numeroDian ?? "").Trim();
        var codigo = (cufe ?? "").Trim();
        if (numero.Length is < 1 or > 50 || numero.Any(char.IsControl)) throw new ReglaDeNegocioException("El número de la factura no es válido.");
        if (codigo.Length is < 10 or > 120 || !codigo.All(char.IsAsciiLetterOrDigit)) throw new ReglaDeNegocioException("El CUFE no es válido.");
        NumeroDian = numero;
        Cufe = codigo;
        FechaEmisionUtc = ahoraUtc;
        Estado = EstadoFactura.Emitida;
    }

    /// <summary>El pago se reembolsó: si aún no se emitió, se anula; si ya se emitió, queda marcada para nota crédito.</summary>
    public void RegistrarReembolso(string nota)
    {
        NotaInterna = nota.Length > 300 ? nota[..300] : nota;
        if (Estado == EstadoFactura.Pendiente) Estado = EstadoFactura.Anulada;
        else if (Estado == EstadoFactura.Emitida) RequiereNotaCredito = true;
    }
}
