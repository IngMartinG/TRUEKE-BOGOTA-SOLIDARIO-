using System.Net;
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
}
