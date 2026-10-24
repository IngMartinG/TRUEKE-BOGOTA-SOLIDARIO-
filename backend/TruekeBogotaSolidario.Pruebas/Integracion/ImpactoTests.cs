using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class ImpactoTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ImpactoTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task CompletarIntercambioAsync(HttpClient duenio, HttpClient otro, string modo)
    {
        var pub = await Api.CrearPublicacionAsync(duenio, modo: modo);
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa" });
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await otro.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Es_publico_y_solo_devuelve_totales_sin_datos_de_personas()
    {
        // Tres intercambios entre tres parejas distintas (el tope anti-farmeo no afecta el conteo)
        var ana = await Api.RegistrarAsync(_fabrica, "impacto-ana");
        var beto = await Api.RegistrarAsync(_fabrica, "impacto-beto");
        var carla = await Api.RegistrarAsync(_fabrica, "impacto-carla");
        await CompletarIntercambioAsync(ana, beto, "Trueke");
        await CompletarIntercambioAsync(beto, carla, "Donacion");
        await CompletarIntercambioAsync(carla, ana, "Compra");

        var r = await _fabrica.CreateClient().GetAsync("/api/v1/impacto"); // sin sesión
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        var d = JsonDocument.Parse(json).RootElement;
        Assert.True(d.GetProperty("objetosConNuevoHogar").GetInt32() >= 3);
        Assert.True(d.GetProperty("truekes").GetInt32() >= 1);
        Assert.True(d.GetProperty("donaciones").GetInt32() >= 1);
        Assert.True(d.GetProperty("compras").GetInt32() >= 1);
        Assert.True(d.GetProperty("personasQueIntercambiaron").GetInt32() >= 3);
        Assert.True(d.GetProperty("esteMes").GetInt32() >= 3);
        // Las publicaciones de prueba son de Chapinero (Bogotá): con 3 intercambios ya aparece la localidad
        var chapinero = d.GetProperty("localidadesMasActivas").EnumerateArray().FirstOrDefault(l => l.GetProperty("localidad").GetString() == "Chapinero");
        Assert.Equal(JsonValueKind.Object, chapinero.ValueKind);
        Assert.Equal("Bogotá, D.C.", chapinero.GetProperty("municipio").GetString());
        // Estimación ambiental: las publicaciones de prueba son de Ropa y calzado (0,5 kg y 5 kg de CO₂ cada una)
        Assert.True(d.GetProperty("kgReutilizados").GetDecimal() >= 1.5m);
        Assert.True(d.GetProperty("kgCo2Evitado").GetDecimal() >= 15m);
        var factores = d.GetProperty("factores").EnumerateArray().ToList();
        Assert.Equal(7, factores.Count);
        Assert.Equal("Ropa y calzado", factores[0].GetProperty("categoria").GetString());
        Assert.Equal(5m, factores[0].GetProperty("kgCo2PorObjeto").GetDecimal());
        // Nada que identifique a alguien
        Assert.DoesNotContain("impacto-", json);
        Assert.DoesNotContain("@", json);
        Assert.DoesNotContain("\"id\"", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task El_perfil_muestra_el_impacto_de_quien_entrega_y_de_quien_recibe()
    {
        var dani = await Api.RegistrarAsync(_fabrica, "impacto-dani");
        var eva = await Api.RegistrarAsync(_fabrica, "impacto-eva");
        var fer = await Api.RegistrarAsync(_fabrica, "impacto-fer");
        await CompletarIntercambioAsync(dani, eva, "Donacion");
        await CompletarIntercambioAsync(fer, dani, "Trueke");

        async Task<JsonElement> ImpactoDe(HttpClient c)
        {
            var id = (await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid();
            return (await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{id}/perfil")).GetProperty("impacto");
        }

        var deDani = await ImpactoDe(dani); // entregó una y recibió otra
        Assert.Equal(2, deDani.GetProperty("objetos").GetInt32());
        Assert.Equal(1m, deDani.GetProperty("kgReutilizados").GetDecimal());
        Assert.Equal(10m, deDani.GetProperty("kgCo2Evitado").GetDecimal());
        var deEva = await ImpactoDe(eva);
        Assert.Equal(1, deEva.GetProperty("objetos").GetInt32());
        Assert.Equal(5m, deEva.GetProperty("kgCo2Evitado").GetDecimal());
        var nuevo = await ImpactoDe(await Api.RegistrarAsync(_fabrica, "impacto-gina"));
        Assert.Equal(0, nuevo.GetProperty("objetos").GetInt32());
    }
}
