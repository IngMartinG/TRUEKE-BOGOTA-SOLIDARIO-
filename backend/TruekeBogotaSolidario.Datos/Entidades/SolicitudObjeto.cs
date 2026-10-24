namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Una publicación de quien solicita que hace parte de la propuesta vigente de un trueke. Siempre es de quien solicita:
/// la elige él al proponer, o el dueño al contraofertar ("prefiero estos de tus objetos").
/// </summary>
public class SolicitudObjeto
{
    private SolicitudObjeto() { } // EF Core

    internal SolicitudObjeto(Guid solicitudId, Guid publicacionId, int orden)
    {
        SolicitudId = solicitudId;
        PublicacionId = publicacionId;
        Orden = orden;
    }

    public Guid SolicitudId { get; private set; }
    public Guid PublicacionId { get; private set; }
    public Publicacion? Publicacion { get; private set; }
    /// <summary>El orden en que se eligieron (el primero es el principal en la tarjeta).</summary>
    public int Orden { get; internal set; }
}
