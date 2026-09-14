namespace TruekeBogotaSolidario.Api.Domain.Entities;

// ==========================================================================
// CAPA DE DOMINIO - Entidad Categoria
// Tipo de producto (ropa, libros, electrodomesticos, etc.) para filtrar
// busquedas, tal como se definio en el Modelo C4 - Nivel 4.
// ==========================================================================
public class Categoria
{
    public int Id { get; set; }
    public string NombreCategoria { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    // Coleccion de navegacion EF Core: una categoria agrupa varias publicaciones.
    public ICollection<Publicacion> Publicaciones { get; set; } = new List<Publicacion>();

    public string ObtenerDetalles() => $"{NombreCategoria} - {Descripcion}";
}
