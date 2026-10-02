using System.Net;
using System.Net.Http.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Recorre cada controller al menos una vez para detectar errores de ejecución (DI, rutas, serialización).</summary>
public class FlujoBasicoTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public FlujoBasicoTests(FabricaApi fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task Registro_login_publicar_solicitar_aceptar()
    {
        var anonimo = _fabrica.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/categorias")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/eco-puntos/politica")).StatusCode);

        var ana = await Api.RegistrarAsync(_fabrica, "ana");
        var beto = await Api.RegistrarAsync(_fabrica, "beto");

        var pub = await Api.CrearPublicacionAsync(ana, lat: 4.6097102, lon: -74.0817500);
        Assert.Equal(HttpStatusCode.OK, (await anonimo.GetAsync("/api/v1/publicaciones")).StatusCode);

        var resp = await beto.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var solicitudId = (await resp.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var aceptar = await ana.PostAsync($"/api/v1/solicitudes/{solicitudId}/aceptar", null);
        Assert.Equal(HttpStatusCode.OK, aceptar.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await beto.GetAsync("/api/v1/eco-puntos/resumen")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await beto.GetAsync("/api/v1/usuarios/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await beto.GetAsync("/api/v1/solicitudes/enviadas")).StatusCode);
    }
}
