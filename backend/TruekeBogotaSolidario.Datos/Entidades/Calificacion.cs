using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Reseña que una parte deja sobre la otra tras un intercambio COMPLETADO. Una por persona y solicitud.
/// La moderación puede ocultar el comentario (las estrellas siguen contando).
/// </summary>
public class Calificacion
{
    public const int DiasParaCalificar = 30;

    private Calificacion() { } // EF Core

    public Calificacion(Guid solicitudId, Guid autorId, Guid calificadoId, int estrellas, string? comentario, DateTime ahoraUtc)
    {
        if (estrellas is < 1 or > 5) throw new ReglaDeNegocioException("La calificación debe estar entre 1 y 5 estrellas.");
        if (autorId == calificadoId) throw new ReglaDeNegocioException("No puedes calificarte a ti mismo.");
        comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        if (comentario is { Length: > 300 }) throw new ReglaDeNegocioException("El comentario admite como máximo 300 caracteres.");
        if (comentario is not null && comentario.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            throw new ReglaDeNegocioException("El comentario contiene caracteres no permitidos.");
        Id = Guid.NewGuid();
        SolicitudId = solicitudId;
        AutorId = autorId;
        CalificadoId = calificadoId;
        Estrellas = estrellas;
        Comentario = comentario;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid SolicitudId { get; private set; }
    public Guid AutorId { get; private set; }
    public Usuario? Autor { get; private set; }
    public Guid CalificadoId { get; private set; }
    public int Estrellas { get; private set; }
    public string? Comentario { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public bool ComentarioOculto { get; private set; }
    public string? MotivoOcultamiento { get; private set; }

    public void OcultarComentario(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Debes indicar el motivo del ocultamiento.");
        if (ComentarioOculto) throw new ReglaDeNegocioException("El comentario ya está oculto.");
        ComentarioOculto = true;
        MotivoOcultamiento = motivo.Trim();
    }
}
