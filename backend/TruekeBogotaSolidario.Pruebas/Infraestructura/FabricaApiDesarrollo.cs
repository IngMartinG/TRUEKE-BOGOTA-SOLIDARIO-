using Microsoft.AspNetCore.Hosting;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>Igual que <see cref="FabricaApi"/> pero en entorno Development (Swagger habilitado).</summary>
public sealed class FabricaApiDesarrollo : FabricaApi
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
    }
}
