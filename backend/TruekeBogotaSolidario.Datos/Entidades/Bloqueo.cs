using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Un usuario bloquea a otro: desde ese momento ninguno de los dos puede escribirle al otro ni solicitar sus publicaciones,
/// y no se ven "escribiendo…" ni "en línea". Las denuncias y calificaciones siguen funcionando (nadie pierde su defensa).
/// </summary>
public class Bloqueo
{
    private Bloqueo() { } // EF Core

    public Bloqueo(Guid bloqueadorId, Guid bloqueadoId, DateTime ahoraUtc)
    {
        if (bloqueadorId == bloqueadoId) throw new ReglaDeNegocioException("No puedes bloquearte a ti mismo.");
        BloqueadorId = bloqueadorId;
        BloqueadoId = bloqueadoId;
        FechaUtc = ahoraUtc;
    }

    public Guid BloqueadorId { get; private set; }
    public Guid BloqueadoId { get; private set; }
    public Usuario? Bloqueado { get; private set; }
    public DateTime FechaUtc { get; private set; }
}
