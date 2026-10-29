using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Impacto 2.0.0: registros por objeto entregado, filtros públicos y factores editables sin reescribir la historia.</summary>
public class ImpactoTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ImpactoTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task<Guid> PublicarAsync(HttpClient c, string modo, string localidad = "Chapinero", decimal? kg = null,
        string? material = null, int categoriaId = 1)
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Objeto de prueba", descripcion = "En buen estado", categoriaId, modo, condicion = "Usado", localidad,
            precioReferenciaCop = modo == "Compra" ? 50000 : (decimal?)null, pesoEstimadoKg = kg, material
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task CompletarAsync(HttpClient duenio, HttpClient otro, Guid pub)
    {
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa" });
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await otro.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
    }

    private static async Task CompletarIntercambioAsync(HttpClient duenio, HttpClient otro, string modo, string localidad = "Chapinero")
        => await CompletarAsync(duenio, otro, await PublicarAsync(duenio, modo, localidad));

    private async Task<JsonElement> ImpactoDeAsync(HttpClient c)
    {
        var id = (await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid();
        return (await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{id}/perfil")).GetProperty("impacto");
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
        Assert.True(d.GetProperty("intercambios").GetInt32() >= 3);
        Assert.True(d.GetProperty("truekes").GetInt32() >= 1);
        Assert.True(d.GetProperty("donaciones").GetInt32() >= 1);
        Assert.True(d.GetProperty("compras").GetInt32() >= 1);
        Assert.True(d.GetProperty("personasQueIntercambiaron").GetInt32() >= 3);
        Assert.True(d.GetProperty("esteMes").GetInt32() >= 3);
        Assert.Equal("2.0.0", d.GetProperty("versionMetodologia").GetString());
        // Las publicaciones de prueba son de Chapinero (Bogotá): con 3 intercambios ya aparece la localidad
        var chapinero = d.GetProperty("localidadesMasActivas").EnumerateArray().FirstOrDefault(l => l.GetProperty("localidad").GetString() == "Chapinero");
        Assert.Equal(JsonValueKind.Object, chapinero.ValueKind);
        Assert.Equal("Bogotá, D.C.", chapinero.GetProperty("municipio").GetString());
        // Ropa y calzado sin peso declarado: 0,5 kg típicos de textil (8 kg de CO₂ por kg) = 4 kg de CO₂ cada uno
        Assert.True(d.GetProperty("kgReutilizados").GetDecimal() >= 1.5m);
        Assert.True(d.GetProperty("kgCo2Evitado").GetDecimal() >= 12m);
        var textil = d.GetProperty("porMaterial").EnumerateArray().Single(m => m.GetProperty("material").GetString() == "Textil");
        Assert.True(textil.GetProperty("objetos").GetInt32() >= 3);
        var factores = d.GetProperty("factores").EnumerateArray().ToList();
        Assert.Equal(9, factores.Count);
        Assert.Equal(8m, factores.Single(f => f.GetProperty("material").GetString() == "Textil").GetProperty("kgCo2PorKg").GetDecimal());
        Assert.Equal(7, d.GetProperty("pesosPorCategoria").GetArrayLength());
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

        var deDani = await ImpactoDeAsync(dani); // entregó una y recibió otra
        Assert.Equal(2, deDani.GetProperty("objetos").GetInt32());
        Assert.Equal(1m, deDani.GetProperty("kgReutilizados").GetDecimal());
        Assert.Equal(8m, deDani.GetProperty("kgCo2Evitado").GetDecimal());
        var deEva = await ImpactoDeAsync(eva);
        Assert.Equal(1, deEva.GetProperty("objetos").GetInt32());
        Assert.Equal(4m, deEva.GetProperty("kgCo2Evitado").GetDecimal());
        var nuevo = await ImpactoDeAsync(await Api.RegistrarAsync(_fabrica, "impacto-gina"));
        Assert.Equal(0, nuevo.GetProperty("objetos").GetInt32());
    }

    [Fact]
    public async Task El_peso_y_el_material_declarados_mandan_y_cambiar_un_factor_no_reescribe_la_historia()
    {
        var hugo = await Api.RegistrarAsync(_fabrica, "impacto-hugo");
        var ines = await Api.RegistrarAsync(_fabrica, "impacto-ines");
        var mesa = await PublicarAsync(hugo, "Donacion", kg: 12m, material: "Madera", categoriaId: 4);
        var detalle = await hugo.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{mesa}");
        Assert.Equal(12m, detalle.GetProperty("pesoEstimadoKg").GetDecimal());
        Assert.Equal("Madera", detalle.GetProperty("material").GetString());
        await CompletarAsync(hugo, ines, mesa);
        Assert.Equal(14.4m, (await ImpactoDeAsync(hugo)).GetProperty("kgCo2Evitado").GetDecimal()); // 12 kg × 1,2

        var super = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PutAsJsonAsync("/api/v1/admin/impacto/factores/Madera",
            new { kgCo2PorKg = 2m, fuente = "Estudio nuevo", version = "2.0.0" })).StatusCode); // la versión debe cambiar
        Assert.Equal(HttpStatusCode.Forbidden, (await hugo.PutAsJsonAsync("/api/v1/admin/impacto/factores/Madera",
            new { kgCo2PorKg = 2m, fuente = "Estudio nuevo", version = "2.1.0" })).StatusCode);
        var r = await super.PutAsJsonAsync("/api/v1/admin/impacto/factores/Madera", new { kgCo2PorKg = 2m, fuente = "Estudio nuevo", version = "2.1.0" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        Assert.Equal(14.4m, (await ImpactoDeAsync(hugo)).GetProperty("kgCo2Evitado").GetDecimal()); // lo registrado no cambia
        var otra = await PublicarAsync(ines, "Donacion", kg: 10m, material: "Madera", categoriaId: 4);
        await CompletarAsync(ines, hugo, otra);
        Assert.Equal(34.4m, (await ImpactoDeAsync(hugo)).GetProperty("kgCo2Evitado").GetDecimal()); // + 10 kg × 2
        var d = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/impacto");
        Assert.Equal("2.1.0", d.GetProperty("versionMetodologia").GetString());
    }

    [Fact]
    public async Task Los_filtros_solo_aceptan_localidades_con_suficientes_intercambios()
    {
        var jose = await Api.RegistrarAsync(_fabrica, "impacto-jose");
        var kata = await Api.RegistrarAsync(_fabrica, "impacto-kata");
        var lina = await Api.RegistrarAsync(_fabrica, "impacto-lina");
        var anonimo = _fabrica.CreateClient();
        await CompletarIntercambioAsync(jose, kata, "Donacion", "Fontibon");
        await CompletarIntercambioAsync(kata, lina, "Donacion", "Fontibon");
        Assert.Equal(HttpStatusCode.BadRequest, (await anonimo.GetAsync("/api/v1/impacto?localidad=Fontibon&municipioCodigo=11001")).StatusCode);

        await CompletarIntercambioAsync(lina, jose, "Trueke", "Fontibon");
        var d = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/impacto?localidad=Fontibon&municipioCodigo=11001&periodo=Mes");
        Assert.Equal(3, d.GetProperty("intercambios").GetInt32());
        Assert.False(d.GetProperty("reservado").GetBoolean());
        Assert.Equal("Fontibon", d.GetProperty("localidad").GetString());
        Assert.Equal("Mes", d.GetProperty("periodo").GetString());
        // Con el modo, la misma localidad baja de 3: se reserva para no revelar quién participó.
        var soloTruekes = await anonimo.GetFromJsonAsync<JsonElement>("/api/v1/impacto?localidad=Fontibon&municipioCodigo=11001&modo=Trueke");
        Assert.True(soloTruekes.GetProperty("reservado").GetBoolean());
        Assert.Equal(0, soloTruekes.GetProperty("intercambios").GetInt32());
    }

    [Fact]
    public async Task Los_objetos_tipicos_ayudan_al_publicar_con_sesion()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/v1/impacto/objetos-tipicos")).StatusCode);
        var c = await Api.RegistrarAsync(_fabrica, "impacto-mia");
        var lista = await c.GetFromJsonAsync<JsonElement>("/api/v1/impacto/objetos-tipicos");
        var nevera = lista.EnumerateArray().Single(o => o.GetProperty("nombre").GetString() == "Nevera");
        Assert.Equal(55m, nevera.GetProperty("kg").GetDecimal());
        Assert.Equal("Electrodomestico", nevera.GetProperty("material").GetString());
    }
}
