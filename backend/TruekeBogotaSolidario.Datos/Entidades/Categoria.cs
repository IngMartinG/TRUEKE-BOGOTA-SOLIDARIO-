using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public class Categoria
{
    private Categoria() { NombreCategoria = ""; Descripcion = ""; } // EF Core

    public Categoria(int id, string nombreCategoria, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombreCategoria)) throw new ReglaDeNegocioException("El nombre de la categoría es obligatorio.");
        Id = id;
        NombreCategoria = nombreCategoria.Trim();
        Descripcion = descripcion?.Trim() ?? "";
    }

    public int Id { get; private set; }
    public string NombreCategoria { get; private set; }
    public string Descripcion { get; private set; }

    public string ObtenerDetalles() => $"{NombreCategoria}: {Descripcion}";
}
