using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio;
using TruekeBogotaSolidario.Negocio.Pagos;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>
/// Contenedor DI con la capa de Negocio real sobre una base InMemory aislada (sin HTTP).
/// Con <paramref name="proveedor"/> se simula la pasarela Wompi (para probar el webhook firmado).
/// </summary>
public sealed class EntornoNegocio : IDisposable
{
    public const string SecretoEventos = "secreto-eventos-pruebas";
    private readonly ServiceProvider _sp;

    public EntornoNegocio(IProveedorPagos? proveedor = null)
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
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var services = new ServiceCollection();
        services.AddLogging();
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

    public void Dispose() => _sp.Dispose();
}
