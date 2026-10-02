using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Comentario público sobre una publicación. Los moderadores pueden ocultarlo (nunca se borra: queda para auditoría).</summary>
public class Comentario
{
    public const int LongitudMaxima = 500;

    private Comentario() { Texto = ""; } // EF Core

    public Comentario(Guid publicacionId, Guid autorId, string texto, DateTime ahoraUtc)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length is < 1 or > LongitudMaxima)
            throw new ReglaDeNegocioException($"El comentario debe tener entre 1 y {LongitudMaxima} caracteres.");
        if (texto.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            throw new ReglaDeNegocioException("El comentario contiene caracteres no permitidos.");

        Id = Guid.NewGuid();
        PublicacionId = publicacionId;
        AutorId = autorId;
        Texto = texto;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Publicacion? Publicacion { get; private set; }
    public Guid AutorId { get; private set; }
    public Usuario? Autor { get; private set; }
    public string Texto { get; private set; }
    public DateTime FechaUtc { get; private set; }

    public bool EstaOculto { get; private set; }
    public string? MotivoOcultamiento { get; private set; }

    public byte[]? RowVersion { get; private set; }

    public void Ocultar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Debes indicar el motivo del ocultamiento.");
        if (EstaOculto) throw new ReglaDeNegocioException("El comentario ya está oculto.");
        EstaOculto = true;
        MotivoOcultamiento = motivo.Trim();
    }

    public void Mostrar()
    {
        if (!EstaOculto) throw new ReglaDeNegocioException("El comentario no está oculto.");
        EstaOculto = false;
        MotivoOcultamiento = null;
    }
}
