using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Contraofertas por turnos (precio en Compra, objetos en Trueke) y truekes de hasta 3 objetos.</summary>
public class ContraofertasTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public ContraofertasTests(FabricaApi f) => _f = f;

    private static async Task<JsonElement> SolicitarAsync(HttpClient c, Guid pedida, object? extra = null, HttpStatusCode esperado = HttpStatusCode.Created)
    {
        var cuerpo = new Dictionary<string, object?> { ["publicacionId"] = pedida, ["mensaje"] = "Me interesa" };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) cuerpo[p.Name] = p.GetValue(extra);
        var r = await c.PostAsJsonAsync("/api/v1/solicitudes", cuerpo);
        Assert.True(r.StatusCode == esperado, await r.Content.ReadAsStringAsync());
        return r.StatusCode == HttpStatusCode.Created ? await r.Content.ReadFromJsonAsync<JsonElement>() : default;
    }

    private static async Task<HttpResponseMessage> ContraofertarAsync(HttpClient c, Guid solicitud, object cuerpo)
        => await c.PostAsJsonAsync($"/api/v1/solicitudes/{solicitud}/contraoferta", cuerpo);

    private static async Task<JsonElement> SolicitudAsync(HttpClient c, string bandeja, Guid id)
        => (await c.GetFromJsonAsync<JsonElement[]>($"/api/v1/solicitudes/{bandeja}"))!.Single(s => s.GetProperty("id").GetGuid() == id);

    private static async Task<string> EstadoAsync(HttpClient duenio, Guid publicacion)
        => (await duenio.GetFromJsonAsync<JsonElement[]>("/api/v1/publicaciones/mias"))!
            .Single(p => p.GetProperty("id").GetGuid() == publicacion).GetProperty("estado").GetString()!;

    private static Guid[] Ids(JsonElement s) => s.GetProperty("ofrecidas").EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task En_una_compra_se_negocia_el_precio_por_turnos_y_se_acepta_la_contraoferta()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCo");
        var beto = await Api.RegistrarAsync(_f, "betoCo");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra"); // publicada en $50.000

        var s = await SolicitarAsync(beto, venta, new { precioPropuestoCop = 40000m });
        var id = s.GetProperty("id").GetGuid();
        Assert.Equal(40000m, s.GetProperty("precioPropuestoCop").GetDecimal());
        Assert.Equal(50000m, s.GetProperty("precioPublicadoCop").GetDecimal());
        Assert.False(s.GetProperty("meTocaResponder").GetBoolean());          // le toca al dueño

        // Beto no puede contraofertar ni aceptar su propia propuesta
        Assert.Equal(HttpStatusCode.BadRequest, (await ContraofertarAsync(beto, id, new { precioPropuestoCop = 41000m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await beto.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).StatusCode);

        // Ana contraoferta; repetir el mismo precio o pedir objetos no vale
        Assert.Equal(HttpStatusCode.BadRequest, (await ContraofertarAsync(ana, id, new { precioPropuestoCop = 40000m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ContraofertarAsync(ana, id, new { publicacionesOfrecidasIds = new[] { venta } })).StatusCode);
        var r = await ContraofertarAsync(ana, id, new { precioPropuestoCop = 45000m });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var tras = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45000m, tras.GetProperty("precioPropuestoCop").GetDecimal());
        Assert.Equal(1, tras.GetProperty("contraofertas").GetInt32());
        Assert.Equal(4, tras.GetProperty("contraofertasRestantes").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).StatusCode); // no acepta lo suyo

        // Ahora le toca a Beto: acepta y el acuerdo queda con ese precio
        Assert.True((await SolicitudAsync(beto, "enviadas", id)).GetProperty("meTocaResponder").GetBoolean());
        var aceptada = await beto.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null);
        Assert.True(aceptada.IsSuccessStatusCode, await aceptada.Content.ReadAsStringAsync());
        var final = await SolicitudAsync(ana, "recibidas", id);
        Assert.Equal("Aceptada", final.GetProperty("estado").GetString());
        Assert.Equal(45000m, final.GetProperty("precioPropuestoCop").GetDecimal());
        Assert.Equal("EnNegociacion", await EstadoAsync(ana, venta));

        var avisos = await ana.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones?pagina=1&tamano=20");
        Assert.Contains(avisos.GetProperty("items").EnumerateArray(), n => n.GetProperty("mensaje").GetString()!.Contains("Aceptaron tu contraoferta"));
    }

    [Fact]
    public async Task En_un_trueke_se_ofrecen_hasta_3_objetos_y_el_duenio_puede_pedir_otros_del_solicitante()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCo2");
        var beto = await Api.RegistrarAsync(_f, "betoCo2");
        var carla = await Api.RegistrarAsync(_f, "carlaCo2");
        var pubAna = await Api.CrearPublicacionAsync(ana);
        var b1 = await Api.CrearPublicacionAsync(beto);
        var b2 = await Api.CrearPublicacionAsync(beto);
        var b3 = await Api.CrearPublicacionAsync(beto);
        var b4 = await Api.CrearPublicacionAsync(beto);
        var deCarla = await Api.CrearPublicacionAsync(carla);

        await SolicitarAsync(beto, pubAna, new { publicacionesOfrecidasIds = new[] { b1, b2, b3, b4 } }, HttpStatusCode.BadRequest); // más de 3
        var s = await SolicitarAsync(beto, pubAna, new { publicacionesOfrecidasIds = new[] { b1, b2 } });
        var id = s.GetProperty("id").GetGuid();
        Assert.Equal(new[] { b1, b2 }, Ids(s));

        // Ana prefiere otro objeto de Beto; uno ajeno o en dinero no vale
        Assert.Equal(HttpStatusCode.NotFound, (await ContraofertarAsync(ana, id, new { publicacionesOfrecidasIds = new[] { deCarla } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ContraofertarAsync(ana, id, new { precioPropuestoCop = 10000m })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ContraofertarAsync(ana, id, new { publicacionesOfrecidasIds = new[] { b2, b1 } })).StatusCode); // la misma
        var r = await ContraofertarAsync(ana, id, new { publicacionesOfrecidasIds = new[] { b1, b3 } });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        Assert.Equal(new[] { b1, b3 }, Ids(await r.Content.ReadFromJsonAsync<JsonElement>()));

        // Beto acepta: lo pedido y los dos objetos acordados quedan reservados; el que salió de la propuesta no
        (await beto.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        Assert.Equal("EnNegociacion", await EstadoAsync(ana, pubAna));
        Assert.Equal("EnNegociacion", await EstadoAsync(beto, b1));
        Assert.Equal("EnNegociacion", await EstadoAsync(beto, b3));
        Assert.Equal("Disponible", await EstadoAsync(beto, b2));

        (await ana.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await beto.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        Assert.Equal("Intercambiada", await EstadoAsync(beto, b1));
        Assert.Equal("Intercambiada", await EstadoAsync(beto, b3));
        Assert.Equal("Disponible", await EstadoAsync(beto, b2));
    }

    [Fact]
    public async Task Si_se_retira_uno_de_los_objetos_acordados_el_trueke_no_se_concreta_y_todo_vuelve_al_catalogo()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCo3");
        var beto = await Api.RegistrarAsync(_f, "betoCo3");
        var pubAna = await Api.CrearPublicacionAsync(ana);
        var b1 = await Api.CrearPublicacionAsync(beto);
        var b2 = await Api.CrearPublicacionAsync(beto);
        var id = (await SolicitarAsync(beto, pubAna, new { publicacionesOfrecidasIds = new[] { b1, b2 } })).GetProperty("id").GetGuid();
        (await ana.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();

        (await beto.PostAsJsonAsync($"/api/v1/publicaciones/{b1}/cancelar", new { motivo = "Se dañó" })).EnsureSuccessStatusCode();
        Assert.Equal("NoConcretada", (await SolicitudAsync(ana, "recibidas", id)).GetProperty("estado").GetString());
        Assert.Equal("Disponible", await EstadoAsync(ana, pubAna));
        Assert.Equal("Disponible", await EstadoAsync(beto, b2));
    }

    [Fact]
    public async Task Hay_maximo_5_contraofertas_y_en_una_donacion_no_hay()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCo4");
        var beto = await Api.RegistrarAsync(_f, "betoCo4");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra");
        var id = (await SolicitarAsync(beto, venta)).GetProperty("id").GetGuid();
        var turnos = new[] { ana, beto, ana, beto, ana };
        for (var i = 0; i < turnos.Length; i++)
        {
            var r = await ContraofertarAsync(turnos[i], id, new { precioPropuestoCop = 30000m + i * 1000 });
            Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        }
        var sexta = await ContraofertarAsync(beto, id, new { precioPropuestoCop = 39000m });
        Assert.Equal(HttpStatusCode.BadRequest, sexta.StatusCode);
        Assert.Contains("5 contraofertas", await sexta.Content.ReadAsStringAsync());
        Assert.Equal(0, (await SolicitudAsync(beto, "enviadas", id)).GetProperty("contraofertasRestantes").GetInt32());
        (await beto.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode(); // aceptar sigue valiendo

        var donacion = await Api.CrearPublicacionAsync(ana, modo: "Donacion");
        var d = (await SolicitarAsync(beto, donacion)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await ContraofertarAsync(ana, d, new { precioPropuestoCop = 1000m })).StatusCode);
        await SolicitarAsync(beto, donacion, new { precioPropuestoCop = 1000m }, HttpStatusCode.BadRequest); // ni precio al pedir
    }
}
