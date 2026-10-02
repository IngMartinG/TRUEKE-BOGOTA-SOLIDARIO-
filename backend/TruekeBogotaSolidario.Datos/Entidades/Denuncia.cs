using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public enum TipoObjetoDenuncia { Publicacion = 1, Comentario = 2, Mensaje = 3, Usuario = 4 }
public enum MotivoDenuncia { Spam = 1, Fraude = 2, ContenidoInapropiado = 3, ArticuloProhibido = 4, Acoso = 5, Otro = 6 }
public enum EstadoDenuncia { Pendiente = 1, Resuelta = 2, Descartada = 3 }

/// <summary>Reporte de un usuario sobre contenido o una persona. Una por denunciante y objetivo; la resuelve un moderador.</summary>
public class Denuncia
{
    private Denuncia() { } // EF Core

    public Denuncia(Guid denuncianteId, TipoObjetoDenuncia tipo, Guid objetivoId, MotivoDenuncia motivo, string? detalle, DateTime ahoraUtc)
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
    }

    public Guid Id { get; private set; }
    public Guid DenuncianteId { get; private set; }
    public TipoObjetoDenuncia Tipo { get; private set; }
    public Guid ObjetivoId { get; private set; }
    public MotivoDenuncia Motivo { get; private set; }
    public string? Detalle { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public EstadoDenuncia Estado { get; private set; }
    public Guid? ModeradorId { get; private set; }
    public DateTime? FechaResolucionUtc { get; private set; }
    public string? NotaResolucion { get; private set; }
    public byte[]? RowVersion { get; private set; }

    public void Resolver(Guid moderadorId, bool procedente, string? nota, DateTime ahoraUtc)
    {
        if (Estado != EstadoDenuncia.Pendiente) throw new ReglaDeNegocioException("La denuncia ya fue resuelta.");
        Estado = procedente ? EstadoDenuncia.Resuelta : EstadoDenuncia.Descartada;
        ModeradorId = moderadorId;
        FechaResolucionUtc = ahoraUtc;
        NotaResolucion = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim() is { Length: > 300 } n ? n[..300] : nota.Trim();
    }
}
