using System.Net;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class SwaggerTests
{
    [Fact]
    public async Task Swagger_se_genera_en_Development()
    {
        await using var fabrica = new FabricaApiDesarrollo();
        var resp = await fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Swagger_no_existe_fuera_de_Development()
    {
        await using var fabrica = new FabricaApi();
        var resp = await fabrica.CreateClient().GetAsync("/swagger/v1/swagger.json");
        Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
    }
}
