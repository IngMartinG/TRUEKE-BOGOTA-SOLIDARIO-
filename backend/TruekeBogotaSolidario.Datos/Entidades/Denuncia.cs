using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public enum TipoObjetoDenuncia { Publicacion = 1, Comentario = 2, Mensaje = 3, Usuario = 4, Calificacion = 5 }
public enum MotivoDenuncia { Spam = 1, Fraude = 2, ContenidoInapropiado = 3, ArticuloProhibido = 4, Acoso = 5, Otro = 6 }
public enum EstadoDenuncia { Pendiente = 1, Resuelta = 2, Descartada = 3 }
public enum AccionModeracion { Descartar = 1, OcultarContenido = 2, MarcarRevisada = 3 }

/// <summary>Reporte de un usuario sobre contenido o una persona. Una por denunciante y objetivo; la resuelve un moderador.</summary>
public class Denuncia
{
    private Denuncia() { } // EF Core

    /// <param name="denunciadoId">Autor del contenido (o el usuario denunciado): a quien se avisa y quien puede apelar.</param>
    public Denuncia(Guid denuncianteId, TipoObjetoDenuncia tipo, Guid objetivoId, MotivoDenuncia motivo, string? detalle, DateTime ahoraUtc,
        Guid? denunciadoId = null)
    {
        if (!Enum.IsDefined(tipo) || !Enum.IsDefined(motivo)) throw new ReglaDeNegocioException("Denuncia no válida.");
        detalle = string.IsNullOrWhiteSpace(detalle) ? null : detalle.Trim();
        if (detalle is { Length: > 500 }) throw new ReglaDeNegocioException("El detalle admite como máximo 500 caracteres.");
        if (detalle is not null && detalle.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            throw new ReglaDeNegocioException("El detalle contiene caracteres no permitidos.");
        if (motivo == MotivoDenuncia.Otro && detalle is null) throw new ReglaDeNegocioException("Describe el motivo de la denuncia.");

        Id = Guid.NewGuid();
        DenuncianteId = denuncianteId;
        Tipo = tipo;
        ObjetivoId = objetivoId;
        Motivo = motivo;
        Detalle = detalle;
        FechaUtc = ahoraUtc;
        Estado = EstadoDenuncia.Pendiente;
        DenunciadoId = denunciadoId;
    }

    public Guid Id { get; private set; }
    public Guid DenuncianteId { get; private set; }
    public TipoObjetoDenuncia Tipo { get; private set; }
    public Guid ObjetivoId { get; private set; }
    public MotivoDenuncia Motivo { get; private set; }
    public string? Detalle { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public EstadoDenuncia Estado { get; private set; }
    /// <summary>Autor del contenido denunciado (null en denuncias anteriores a este campo: se completa al resolver).</summary>
    public Guid? DenunciadoId { get; private set; }
    public Guid? ModeradorId { get; private set; }
    public DateTime? FechaResolucionUtc { get; private set; }
    public string? NotaResolucion { get; private set; }
    public AccionModeracion? Accion { get; private set; }
    /// <summary>Todas las denuncias resueltas juntas (mismo objetivo) comparten este id: es lo que el denunciado puede apelar.</summary>
    public Guid? ResolucionId { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void Resolver(Guid moderadorId, AccionModeracion accion, string? nota, DateTime ahoraUtc, Guid resolucionId, Guid? denunciadoId)
    {
        if (Estado != EstadoDenuncia.Pendiente) throw new ReglaDeNegocioException("La denuncia ya fue resuelta.");
        if (!Enum.IsDefined(accion)) throw new ReglaDeNegocioException("Acción no válida.");
        Estado = accion == AccionModeracion.Descartar ? EstadoDenuncia.Descartada : EstadoDenuncia.Resuelta;
        Accion = accion;
        ModeradorId = moderadorId;
        FechaResolucionUtc = ahoraUtc;
        ResolucionId = resolucionId;
        DenunciadoId ??= denunciadoId;
        NotaResolucion = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim() is { Length: > 300 } n ? n[..300] : nota.Trim();
    }
}
