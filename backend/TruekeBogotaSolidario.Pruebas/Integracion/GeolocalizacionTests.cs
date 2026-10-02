using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class GeolocalizacionTests : IClassFixture<FabricaApi>
{
    private const double LatExacta = 4.6097102, LonExacta = -74.0817500;
    private readonly FabricaApi _fabrica;
    public GeolocalizacionTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonElement Buscar(JsonElement lista, Guid id, bool envuelto = false)
        => lista.EnumerateArray().Select(e => envuelto ? e.GetProperty("publicacion") : e)
            .First(e => e.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task Listado_publico_muestra_coordenadas_aproximadas_y_el_duenio_las_exactas()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "duenio");
        var id = await Api.CrearPublicacionAsync(duenio, LatExacta, LonExacta);

        var publico = Buscar((await Json(await _fabrica.CreateClient().GetAsync("/api/v1/publicaciones?tamano=50"))).GetProperty("items"), id);
        Assert.Equal(4.61, publico.GetProperty("latitud").GetDouble());
        Assert.Equal(-74.08, publico.GetProperty("longitud").GetDouble());
        Assert.True(publico.GetProperty("coordenadasAproximadas").GetBoolean());

        var detalleAnonimo = await Json(await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{id}"));
        Assert.Equal(4.61, detalleAnonimo.GetProperty("latitud").GetDouble());

        var mia = await Json(await duenio.GetAsync($"/api/v1/publicaciones/{id}"));
        Assert.Equal(LatExacta, mia.GetProperty("latitud").GetDouble());
        Assert.Equal(LonExacta, mia.GetProperty("longitud").GetDouble());
        Assert.False(mia.GetProperty("coordenadasAproximadas").GetBoolean());
    }

    [Fact]
    public async Task Con_solicitud_aceptada_el_solicitante_ve_coordenadas_exactas()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "duenio2");
        var otro = await Api.RegistrarAsync(_fabrica, "otro2");
        var id = await Api.CrearPublicacionAsync(duenio, LatExacta, LonExacta);

        var antes = await Json(await otro.GetAsync($"/api/v1/publicaciones/{id}"));
        Assert.Equal(4.61, antes.GetProperty("latitud").GetDouble());

        var sol = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = id, mensaje = "Hola" });
        var solId = (await sol.Content.ReadFromJsonAsync<IdDto>())!.Id;
        (await duenio.PostAsync($"/api/v1/solicitudes/{solId}/aceptar", null)).EnsureSuccessStatusCode();

        var despues = await Json(await otro.GetAsync($"/api/v1/publicaciones/{id}"));
        Assert.Equal(LatExacta, despues.GetProperty("latitud").GetDouble());
    }

    [Fact]
    public async Task Cercanas_filtra_por_radio_ordena_por_distancia_y_no_expone_coordenadas_exactas()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "geo");
        var cerca = await Api.CrearPublicacionAsync(duenio, LatExacta, LonExacta);            // centro de Bogotá
        var medio = await Api.CrearPublicacionAsync(duenio, 4.6486, -74.0628);                 // ~5 km al norte
        var lejos = await Api.CrearPublicacionAsync(duenio, 6.2518, -75.5636);                 // Medellín

        var lista = await Json(await _fabrica.CreateClient().GetAsync(FormattableString.Invariant($"/api/v1/publicaciones/cercanas?lat={LatExacta}&lon={LonExacta}&radioKm=10&max=50")));
        var ids = lista.EnumerateArray().Select(e => e.GetProperty("publicacion").GetProperty("id").GetGuid()).ToList();
        Assert.Contains(cerca, ids);
        Assert.Contains(medio, ids);
        Assert.DoesNotContain(lejos, ids);
        Assert.True(ids.IndexOf(cerca) < ids.IndexOf(medio));

        var item = Buscar(lista, cerca, envuelto: true);
        Assert.Equal(4.61, item.GetProperty("latitud").GetDouble());
        // la distancia se mide contra la posición aproximada: nunca 0 exacto respecto del punto real
        var distancia = lista.EnumerateArray().First(e => e.GetProperty("publicacion").GetProperty("id").GetGuid() == cerca).GetProperty("distanciaKm").GetDouble();
        Assert.True(distancia > 0);
    }

    [Theory]
    [InlineData("/api/v1/publicaciones/cercanas?lon=-74.08")]
    [InlineData("/api/v1/publicaciones/cercanas?lat=4.6&lon=-74.08&radioKm=500")]
    [InlineData("/api/v1/publicaciones/cercanas?lat=95&lon=-74.08")]
    public async Task Cercanas_valida_parametros(string url)
        => Assert.Equal(HttpStatusCode.BadRequest, (await _fabrica.CreateClient().GetAsync(url)).StatusCode);
}

