using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Negocio;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Pagos;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>
/// Contenedor DI con la capa de Negocio real sobre una base InMemory aislada (sin HTTP).
/// Con <paramref name="proveedor"/> se simula la pasarela Wompi (para probar el webhook firmado).
/// </summary>
public sealed class EntornoNegocio : IDisposable
{
    public const string SecretoEventos = "secreto-eventos-pruebas";
    private readonly ServiceProvider _sp;

    public EntornoNegocio(IProveedorPagos? proveedor = null, TimeProvider? reloj = null)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Database:Provider"] = "InMemory",
            ["Database:NombreInMemory"] = "negocio-" + Guid.NewGuid().ToString("N"),
            ["Jwt:Issuer"] = "pruebas",
            ["Jwt:Audience"] = "pruebas",
            ["Jwt:Key"] = FabricaApi.ClaveJwtPruebas,
            ["Seguridad:SegundosCacheSesion"] = "0",
            ["Pagos:Proveedor"] = proveedor is null ? "Simulado" : "Wompi",
            ["Pagos:Wompi:BaseUrl"] = "https://sandbox.wompi.co/v1",
            ["Pagos:Wompi:LlavePublica"] = "pub_test_x",
            ["Pagos:Wompi:SecretoIntegridad"] = "integridad-pruebas",
            ["Pagos:Wompi:SecretoEventos"] = SecretoEventos,
            ["Correo:Proveedor"] = "Simulado",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        if (reloj is not null) services.AddSingleton(reloj); // AddNegocio usa TryAdd: respeta este reloj
        services.AddNegocio(config);
        if (proveedor is not null) services.Replace(ServiceDescriptor.Singleton(proveedor));
        _sp = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        NegocioServiceCollectionExtensions.InicializarBaseDatosAsync(_sp, new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Database:Inicializacion"] = "EnsureCreated" }).Build()).GetAwaiter().GetResult();
    }

    /// <summary>Cada llamada = un "request" nuevo (scope y DbContext nuevos), como en producción.</summary>
    public async Task<TResultado> EnScopeAsync<TServicio, TResultado>(Func<TServicio, Task<TResultado>> accion) where TServicio : notnull
    {
        using var scope = _sp.CreateScope();
        return await accion(scope.ServiceProvider.GetRequiredService<TServicio>());
    }

    public async Task EnScopeAsync<TServicio>(Func<TServicio, Task> accion) where TServicio : notnull
    {
        using var scope = _sp.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<TServicio>());
    }

    /// <summary>Registra un usuario (con el correo ya verificado, para que pueda operar) y devuelve su sesión.</summary>
    public async Task<SesionDto> RegistrarAsync(string nombre)
    {
        var r = await EnScopeAsync<IAuthService, ResultadoAutenticacion>(a => a.RegistrarAsync(new RegistroRequest
        {
            NombreCompleto = nombre + " Prueba", Localidad = "Kennedy", Correo = $"{nombre}{Guid.NewGuid():N}@t.co",
            Clave = "Clave12345", AceptoPoliticaDatos = true
        }));
        await MarcarCorreoVerificadoAsync(r.Sesion.Usuario.Id);
        return r.Sesion;
    }

    /// <summary>Atajo de pruebas: marca el correo como verificado directamente en la base.</summary>
    public async Task MarcarCorreoVerificadoAsync(Guid usuarioId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
        (await db.Usuarios.FindAsync(usuarioId))!.MarcarCorreoVerificado(DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    public IServiceProvider Proveedor => _sp;

    public void Dispose() => _sp.Dispose();
}
