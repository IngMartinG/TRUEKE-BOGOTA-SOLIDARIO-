using System.Net;
using Microsoft.AspNetCore.Hosting;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class ArranqueTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ArranqueTests(FabricaApi fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task La_api_arranca_y_responde_liveness_y_readiness()
    {
        var cliente = _fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Arranca_con_Application_Insights_activado()
    {
        // Endpoint de ingesta inexistente: solo se verifica que la configuración del monitoreo no rompa el arranque.
        await using var f = _fabrica.WithWebHostBuilder(b => b.UseSetting("Monitoreo:ConnectionString",
            "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:9/"));
        Assert.Equal(HttpStatusCode.OK, (await f.CreateClient().GetAsync("/health/live")).StatusCode);
    }
}
