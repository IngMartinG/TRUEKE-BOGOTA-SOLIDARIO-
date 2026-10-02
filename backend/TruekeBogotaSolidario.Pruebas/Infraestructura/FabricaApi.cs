using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>
/// Levanta la API real en memoria (entorno "Testing") con base InMemory aislada por instancia y pagos simulados.
/// Se usa UseSetting porque Program.cs lee la configuración ANTES de Build().
/// </summary>
public class FabricaApi : WebApplicationFactory<Program>
{
    public const string ClaveJwtPruebas = "clave-de-pruebas-de-al-menos-32-caracteres-0123456789";
    public const string CorreoSuper = "super@trueke.test";
    public const string ClaveSuper = "SuperClave123";
    /// <summary>32 bytes de prueba en base64 (solo para tests).</summary>
    public const string ClaveCifradoPruebas = "MDEyMzQ1Njc4OWFiY2RlZjAxMjM0NTY3ODlhYmNkZWY=";

    private readonly string _nombreBd = "pruebas-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Provider", "InMemory");
        builder.UseSetting("Database:NombreInMemory", _nombreBd);
        builder.UseSetting("Database:Inicializacion", "EnsureCreated");
        builder.UseSetting("Pagos:Proveedor", "Simulado");
        builder.UseSetting("Jwt:Key", ClaveJwtPruebas);
        builder.UseSetting("Seguridad:SegundosCacheSesion", "0");
        builder.UseSetting("Bootstrap:SuperUsuarioCorreo", CorreoSuper);
        builder.UseSetting("Bootstrap:SuperUsuarioClave", ClaveSuper);
        builder.UseSetting("Cors:Origenes:0", "http://localhost:4200");
        builder.UseSetting("RateLimiting:AuthPorMinuto", "1000");
        builder.UseSetting("RateLimiting:GlobalPorMinuto", "10000");
        builder.UseSetting("RateLimiting:EscrituraPorMinuto", "1000");
        builder.UseSetting("RateLimiting:RefrescoPorMinuto", "1000");
        builder.UseSetting("Seguridad:SegundosGraciaRefresco", "0");
        builder.UseSetting("Correo:Proveedor", "Simulado");
        builder.UseSetting("Seguridad:ClaveCifrado", ClaveCifradoPruebas);
        builder.UseSetting("Seguridad:ExigirDosFactoresModeradores", "false"); // una prueba dedicada lo activa
        builder.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<ICorreoSaliente>(Correos)));
    }

    /// <summary>Correos "enviados" por esta instancia de la API.</summary>
    public CorreoCapturado Correos { get; } = new();
}
