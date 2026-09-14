namespace TruekeBogotaSolidario.Api.Domain.Entities;

// ==========================================================================
// CAPA DE DOMINIO - Entidad Usuario
// Persona duena de la cuenta, puede publicar bienes y solicitar intercambios.
// ==========================================================================
public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NombreCompleto { get; set; } = string.Empty;
    public string Localidad { get; set; } = string.Empty; // Localidad de Bogota (Suba, Kennedy, Chapinero, etc.)
    public string Correo { get; set; } = string.Empty;

    public ICollection<Publicacion> Publicaciones { get; set; } = new List<Publicacion>();
    public ICollection<Solicitud> SolicitudesEnviadas { get; set; } = new List<Solicitud>();

    // Metodos de dominio definidos en el C4 Nivel 4. La orquestacion real
    // (persistir, validar duplicados, etc.) vive en la capa de Servicio;
    // aqui se representa la intencion/regla de negocio propia de la entidad.
    public void PublicarNuevoBien(Publicacion publicacion)
    {
        publicacion.UsuarioId = Id;
        publicacion.Publicar();
        Publicaciones.Add(publicacion);
    }

    public Solicitud SolicitarIntercambio(Publicacion publicacion, string mensaje)
    {
        if (publicacion.UsuarioId == Id)
        {
            throw new InvalidOperationException("Un usuario no puede solicitar el intercambio de su propia publicacion.");
        }

        var solicitud = new Solicitud
        {
            UsuarioId = Id,
            PublicacionId = publicacion.Id,
            Mensaje = mensaje,
            FechaSolicitud = DateTime.UtcNow
        };

        SolicitudesEnviadas.Add(solicitud);
        return solicitud;
    }
}
