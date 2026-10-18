using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Petición, queja, reclamo o sugerencia (Ley 1755 de 2015), consulta o reclamo sobre datos personales (Ley 1581 de 2012,
/// arts. 14 y 15) y solicitudes de retracto o reversión del pago (Ley 1480 de 2011, arts. 47 y 51). Se responde por
/// escrito dentro de <see cref="DiasHabilesPara"/> días hábiles colombianos. La radica una persona con cuenta o, por el
/// canal público, cualquier persona con su nombre y correo.
/// </summary>
public class Pqr
{
    public const int DiasHabilesRespuesta = 15;
    public const int DiasHabilesConsulta = 10;
    public const int LongitudMaximaTexto = 2000;

    private Pqr() { Radicado = ""; Asunto = ""; Descripcion = ""; } // EF Core

    /// <summary>Plazo de respuesta según el tipo: 10 días hábiles para una consulta de datos personales, 15 para lo demás.</summary>
    public static int DiasHabilesPara(TipoPqr tipo) => tipo == TipoPqr.Consulta ? DiasHabilesConsulta : DiasHabilesRespuesta;

    /// <summary>Canal público (sin cuenta): no admite retracto ni reversión, que se piden sobre un pago de la propia cuenta.</summary>
    public static Pqr CrearPublica(string nombre, string correo, TipoPqr tipo, string asunto, string descripcion, DateTime ahoraUtc)
    {
        if (tipo is TipoPqr.Retracto or TipoPqr.ReversionPago)
            throw new ReglaDeNegocioException("El retracto y la reversión de un pago se piden desde tu cuenta, en Soporte y PQR.");
        var n = FormatosTexto.Compactar(nombre);
        if (n.Length is < 3 or > 120 || !FormatosTexto.EsNombrePersona(n)) throw new ReglaDeNegocioException(FormatosTexto.MensajeNombrePersona);
        var c = Usuario.NormalizarCorreo(correo);
        if (c.Length is < 5 or > 160 || c.IndexOf('@') <= 0) throw new ReglaDeNegocioException("El correo no es válido.");
        return new Pqr(null, tipo, asunto, descripcion, null, ahoraUtc) { NombreSolicitante = n, CorreoSolicitante = c };
    }

    public Pqr(Guid? usuarioId, TipoPqr tipo, string asunto, string descripcion, string? pagoReferencia, DateTime ahoraUtc)
    {
        if (!Enum.IsDefined(tipo)) throw new ReglaDeNegocioException("Tipo de solicitud no válido.");
        var a = (asunto ?? "").Trim();
        var d = (descripcion ?? "").Trim();
        if (a.Length is < 5 or > 120 || !TextoSeguro.EsSeguro(a)) throw new ReglaDeNegocioException("El asunto debe tener entre 5 y 120 caracteres.");
        if (d.Length is < 10 or > LongitudMaximaTexto) throw new ReglaDeNegocioException($"La descripción debe tener entre 10 y {LongitudMaximaTexto} caracteres.");
        if (!TextoSeguro.EsSeguro(d, multilinea: true)) throw new ReglaDeNegocioException("La descripción contiene caracteres no permitidos.");
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
        FechaLimiteUtc = SumarDiasHabiles(ahoraUtc, DiasHabilesPara(tipo));
    }

    public Guid Id { get; private set; }
    /// <summary>Número que se le entrega al usuario para hacer seguimiento.</summary>
    public string Radicado { get; private set; }
    /// <summary>Null si se radicó por el canal público, sin cuenta.</summary>
    public Guid? UsuarioId { get; private set; }
    /// <summary>Solo en el canal público: a quién responder.</summary>
    public string? NombreSolicitante { get; private set; }
    public string? CorreoSolicitante { get; private set; }
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
    public uint RowVersion { get; private set; }

    public bool Pendiente => Estado is EstadoPqr.Abierta or EstadoPqr.EnTramite;

    /// <summary>El equipo empezó a revisarla: se le avisa a quien la radicó.</summary>
    public void MarcarEnTramite()
    {
        if (Estado != EstadoPqr.Abierta) throw new ReglaDeNegocioException("Solo una solicitud abierta puede pasar a trámite.");
        Estado = EstadoPqr.EnTramite;
    }

    public void Responder(Guid moderadorId, string respuesta, DateTime ahoraUtc)
    {
        if (!Pendiente) throw new ReglaDeNegocioException("Esta solicitud ya fue respondida.");
        var r = (respuesta ?? "").Trim();
        if (!TextoSeguro.EsSeguro(r, multilinea: true)) throw new ReglaDeNegocioException("La respuesta contiene caracteres no permitidos.");
        if (r.Length is < 10 or > LongitudMaximaTexto) throw new ReglaDeNegocioException($"La respuesta debe tener entre 10 y {LongitudMaximaTexto} caracteres.");
        Respuesta = r;
        RespondidaPorId = moderadorId;
        FechaRespuestaUtc = ahoraUtc;
        Estado = EstadoPqr.Respondida;
    }

    /// <summary>Suma días hábiles colombianos (sin fines de semana ni festivos nacionales).</summary>
    public static DateTime SumarDiasHabiles(DateTime desdeUtc, int dias) => DiasHabiles.Sumar(desdeUtc, dias);
}
