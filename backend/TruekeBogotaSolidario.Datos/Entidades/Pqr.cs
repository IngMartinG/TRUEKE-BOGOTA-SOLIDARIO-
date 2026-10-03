using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Petición, queja, reclamo o sugerencia (Ley 1755 de 2015) y solicitudes de retracto o reversión del pago
/// (Ley 1480 de 2011, arts. 47 y 51). Se responde por escrito dentro de <see cref="DiasHabilesRespuesta"/> días hábiles.
/// </summary>
public class Pqr
{
    public const int DiasHabilesRespuesta = 15;
    public const int LongitudMaximaTexto = 2000;

    private Pqr() { Radicado = ""; Asunto = ""; Descripcion = ""; } // EF Core

    public Pqr(Guid usuarioId, TipoPqr tipo, string asunto, string descripcion, string? pagoReferencia, DateTime ahoraUtc)
    {
        if (!Enum.IsDefined(tipo)) throw new ReglaDeNegocioException("Tipo de solicitud no válido.");
        var a = (asunto ?? "").Trim();
        var d = (descripcion ?? "").Trim();
        if (a.Length is < 5 or > 120 || a.Any(char.IsControl)) throw new ReglaDeNegocioException("El asunto debe tener entre 5 y 120 caracteres.");
        if (d.Length is < 10 or > LongitudMaximaTexto) throw new ReglaDeNegocioException($"La descripción debe tener entre 10 y {LongitudMaximaTexto} caracteres.");
        if (d.Any(c => char.IsControl(c) && c is not '\n' and not '\r')) throw new ReglaDeNegocioException("La descripción contiene caracteres no permitidos.");
        if (tipo is TipoPqr.Retracto or TipoPqr.ReversionPago && string.IsNullOrWhiteSpace(pagoReferencia))
            throw new ReglaDeNegocioException("Indica la referencia del pago (TRK-…).");

        Id = Guid.NewGuid();
        Radicado = $"PQR-{ahoraUtc:yyyyMMdd}-{Id.ToString("N")[..6].ToUpperInvariant()}";
        UsuarioId = usuarioId;
        Tipo = tipo;
        Asunto = a;
        Descripcion = d;
        PagoReferencia = string.IsNullOrWhiteSpace(pagoReferencia) ? null : pagoReferencia.Trim();
        Estado = EstadoPqr.Abierta;
        FechaUtc = ahoraUtc;
        FechaLimiteUtc = SumarDiasHabiles(ahoraUtc, DiasHabilesRespuesta);
    }

    public Guid Id { get; private set; }
    /// <summary>Número que se le entrega al usuario para hacer seguimiento.</summary>
    public string Radicado { get; private set; }
    public Guid UsuarioId { get; private set; }
    public TipoPqr Tipo { get; private set; }
    public string Asunto { get; private set; }
    public string Descripcion { get; private set; }
    public string? PagoReferencia { get; private set; }
    public EstadoPqr Estado { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public DateTime FechaLimiteUtc { get; private set; }
    public string? Respuesta { get; private set; }
    public DateTime? FechaRespuestaUtc { get; private set; }
    public Guid? RespondidaPorId { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void Responder(Guid moderadorId, string respuesta, DateTime ahoraUtc)
    {
        if (Estado != EstadoPqr.Abierta) throw new ReglaDeNegocioException("Esta solicitud ya fue respondida.");
        var r = (respuesta ?? "").Trim();
        if (r.Length is < 10 or > LongitudMaximaTexto) throw new ReglaDeNegocioException($"La respuesta debe tener entre 10 y {LongitudMaximaTexto} caracteres.");
        Respuesta = r;
        RespondidaPorId = moderadorId;
        FechaRespuestaUtc = ahoraUtc;
        Estado = EstadoPqr.Respondida;
    }

    /// <summary>Suma días hábiles (lunes a viernes). Los festivos colombianos no se descuentan: el plazo real es igual o mayor.</summary>
    public static DateTime SumarDiasHabiles(DateTime desde, int dias)
    {
        var fecha = desde;
        var sumados = 0;
        while (sumados < dias)
        {
            fecha = fecha.AddDays(1);
            if (fecha.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) sumados++;
        }
        return fecha;
    }
}
