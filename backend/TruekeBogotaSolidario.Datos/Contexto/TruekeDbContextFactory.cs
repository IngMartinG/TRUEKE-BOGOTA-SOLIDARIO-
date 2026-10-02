using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruekeBogotaSolidario.Datos.Contexto;

/// <summary>
/// Solo para herramientas de diseño (dotnet ef migrations add ...). Lee la cadena de la variable de entorno
/// ConnectionStrings__TruekeDb; si no existe usa LocalDB solo para poder generar el esquema.
/// </summary>
public sealed class TruekeDbContextFactory : IDesignTimeDbContextFactory<TruekeDbContext>
{
    public TruekeDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TruekeDb")
                     ?? "Server=(localdb)\\MSSQLLocalDB;Database=TruekeDesign;Trusted_Connection=True;";
        var opciones = new DbContextOptionsBuilder<TruekeDbContext>().UseSqlServer(cadena).Options;
        return new TruekeDbContext(opciones);
    }
}
