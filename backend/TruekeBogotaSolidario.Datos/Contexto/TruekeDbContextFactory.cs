using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TruekeBogotaSolidario.Datos.Contexto;

/// <summary>
/// Solo para herramientas de diseño (dotnet ef migrations add ...). Lee la cadena de la variable de entorno
/// ConnectionStrings__TruekeDb; si no existe usa una cadena local solo para poder generar el esquema (no se conecta).
/// </summary>
public sealed class TruekeDbContextFactory : IDesignTimeDbContextFactory<TruekeDbContext>
{
    public TruekeDbContext CreateDbContext(string[] args)
    {
        var cadena = Environment.GetEnvironmentVariable("ConnectionStrings__TruekeDb")
                     ?? "Host=localhost;Database=trueke_diseno;Username=postgres";
        var opciones = new DbContextOptionsBuilder<TruekeDbContext>().UseNpgsql(cadena).Options;
        return new TruekeDbContext(opciones);
    }
}
