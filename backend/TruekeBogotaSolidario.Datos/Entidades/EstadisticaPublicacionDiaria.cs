namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Vistas de una publicación por día (UTC). Tabla aparte de Publicacion a propósito: contar una vista no debe
/// cambiar el RowVersion de la publicación (si no, el dueño recibiría 409 al editar mientras otros la miran).
/// </summary>
public class EstadisticaPublicacionDiaria
{
    private EstadisticaPublicacionDiaria() { } // EF Core

    public EstadisticaPublicacionDiaria(Guid publicacionId, DateTime fechaUtc, int vistas)
    {
        PublicacionId = publicacionId;
        Fecha = fechaUtc.Date;
        Vistas = Math.Max(0, vistas);
    }

    public Guid PublicacionId { get; private set; }
    public DateTime Fecha { get; private set; }
    public int Vistas { get; private set; }

    public void Sumar(int vistas) => Vistas = checked(Vistas + Math.Max(0, vistas));
}
