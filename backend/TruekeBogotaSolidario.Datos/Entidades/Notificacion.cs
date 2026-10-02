namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Bandeja de notificaciones: persiste lo que se envía en tiempo real para quien estaba desconectado.</summary>
public class Notificacion
{
    private Notificacion() { Tipo = ""; Mensaje = ""; } // EF Core

    public Notificacion(Guid usuarioId, string tipo, string mensaje, Guid? recursoId, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Tipo = tipo.Length > 40 ? tipo[..40] : tipo;
        Mensaje = mensaje.Length > 300 ? mensaje[..300] : mensaje;
        RecursoId = recursoId;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Tipo { get; private set; }
    public string Mensaje { get; private set; }
    public Guid? RecursoId { get; private set; }
    public DateTime FechaUtc { get; private set; }
    public DateTime? LeidaUtc { get; private set; }

    public void MarcarLeida(DateTime ahoraUtc) => LeidaUtc ??= ahoraUtc;
}
