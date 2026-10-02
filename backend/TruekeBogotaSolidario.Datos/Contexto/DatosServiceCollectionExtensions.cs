using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;

namespace TruekeBogotaSolidario.Datos.Contexto;

public static class DatosServiceCollectionExtensions
{
    /// <param name="proveedor">"InMemory" (desarrollo/pruebas) o "SqlServer".</param>
    /// <param name="nombreInMemory">Nombre de la base InMemory (las pruebas usan uno distinto por instancia para aislarse).</param>
    public static IServiceCollection AddDatos(this IServiceCollection services, string proveedor, string? cadenaConexion, string? nombreInMemory = null)
    {
        var p = (proveedor ?? "").Trim().ToLowerInvariant();
        if (p != "inmemory" && p != "sqlserver")
            throw new InvalidOperationException($"Database:Provider inválido: '{proveedor}'. Usa 'InMemory' o 'SqlServer'.");
        if (p == "sqlserver" && string.IsNullOrWhiteSpace(cadenaConexion))
            throw new InvalidOperationException("Falta ConnectionStrings:TruekeDb para usar SQL Server.");

        services.AddDbContext<TruekeDbContext>(o =>
        {
            if (p == "inmemory") o.UseInMemoryDatabase(string.IsNullOrWhiteSpace(nombreInMemory) ? "TruekeDb" : nombreInMemory);
            else o.UseSqlServer(cadenaConexion!, sql => sql.EnableRetryOnFailure());
        });

        services.AddScoped<IUnidadDeTrabajo, UnidadDeTrabajo>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<ICategoriaRepository, CategoriaRepository>();
        services.AddScoped<IPublicacionRepository, PublicacionRepository>();
        services.AddScoped<ISolicitudRepository, SolicitudRepository>();
        services.AddScoped<ITransaccionRepository, TransaccionRepository>();
        services.AddScoped<IPagoRepository, PagoRepository>();
        services.AddScoped<ISaludBaseDatos, SaludBaseDatos>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<IComentarioRepository, ComentarioRepository>();
        services.AddScoped<ISesionRefreshRepository, SesionRefreshRepository>();
        services.AddScoped<ITokenUsoUnicoRepository, TokenUsoUnicoRepository>();
        return services;
    }
}

public static class DatosInicializador
{
    /// <param name="modo">"Migrate" (producción, requiere migraciones generadas), "EnsureCreated" (primer despliegue/dev) o "None".</param>
    public static async Task InicializarAsync(IServiceProvider proveedor, string modo, string? superCorreo, string? superClave)
    {
        using var scope = proveedor.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();

        switch ((modo ?? "").Trim().ToLowerInvariant())
        {
            case "migrate":
                if (!db.Database.GetMigrations().Any())
                    throw new InvalidOperationException("Database:Inicializacion=Migrate pero no hay migraciones. Ejecuta: dotnet ef migrations add Inicial --project TruekeBogotaSolidario.Datos");
                await db.Database.MigrateAsync();
                break;
            case "ensurecreated":
                await db.Database.EnsureCreatedAsync();
                break;
            case "none":
                break;
            default:
                throw new InvalidOperationException($"Database:Inicializacion inválido: '{modo}'. Usa Migrate, EnsureCreated o None.");
        }

        if (string.IsNullOrWhiteSpace(superCorreo) || string.IsNullOrWhiteSpace(superClave)) return;
        var correo = Usuario.NormalizarCorreo(superCorreo);
        if (await db.Usuarios.AnyAsync(u => u.Correo == correo)) return;

        var su = new Usuario("Super Usuario Trueke", "Bogotá", correo, superClave);
        su.CambiarRol(RolUsuarioEnum.SuperUsuario);
        su.MarcarCorreoVerificado(DateTime.UtcNow); // lo define el operador por configuración
        db.Usuarios.Add(su);
        await db.SaveChangesAsync();
    }
}
