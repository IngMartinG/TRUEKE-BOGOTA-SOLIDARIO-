namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Publicación guardada por un usuario. Clave compuesta (usuario, publicación): no hay duplicados.</summary>
public class Favorito
{
    private Favorito() { } // EF Core

    public Favorito(Guid usuarioId, Guid publicacionId, DateTime ahoraUtc)
    {
        UsuarioId = usuarioId;
        PublicacionId = publicacionId;
        FechaUtc = ahoraUtc;
    }

    public Guid UsuarioId { get; private set; }
    public Guid PublicacionId { get; private set; }
    public DateTime FechaUtc { get; private set; }
}
