using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Ventas que también aceptan trueke: se paga el precio o se ofrecen objetos, y cuenta el modo en que se cerró.</summary>
public class VentaAceptaTruekeTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public VentaAceptaTruekeTests(FabricaApi f) => _f = f;

    private static async Task<HttpResponseMessage> SolicitarAsync(HttpClient c, Guid pedida, object? extra = null)
    {
        var cuerpo = new Dictionary<string, object?> { ["publicacionId"] = pedida, ["mensaje"] = "Me interesa" };
        if (extra is not null)
            foreach (var p in extra.GetType().GetProperties()) cuerpo[p.Name] = p.GetValue(extra);
        return await c.PostAsJsonAsync("/api/v1/solicitudes", cuerpo);
    }

    private static async Task<JsonElement> CreadaAsync(HttpResponseMessage r)
    {
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> ContraofertarAsync(HttpClient c, Guid solicitud, object cuerpo)
    {
        var r = await c.PostAsJsonAsync($"/api/v1/solicitudes/{solicitud}/contraoferta", cuerpo);
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task CompletarAsync(HttpClient duenio, HttpClient solicitante, Guid solicitud)
    {
        (await duenio.PostAsync($"/api/v1/solicitudes/{solicitud}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await solicitante.PostAsync($"/api/v1/solicitudes/{solicitud}/confirmar-entrega", null)).EnsureSuccessStatusCode();
    }

    /// <summary>Eco-Puntos que sumó el intercambio de esa solicitud.</summary>
    private static async Task<int> PuntosPorAsync(HttpClient c, Guid solicitud)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/eco-puntos/movimientos?pagina=1&tamano=50"))
            .GetProperty("items").EnumerateArray()
            .Single(m => m.GetProperty("concepto").GetString() == "Intercambio" && m.GetProperty("referenciaId").GetGuid() == solicitud)
            .GetProperty("puntos").GetInt32();

    [Fact]
    public async Task Solo_una_venta_puede_aceptar_trueke_y_aparece_tambien_en_el_filtro_de_trueke()
    {
        var ana = await Api.RegistrarAsync(_f, "anaVt");
        var marca = "Guitarra" + Guid.NewGuid().ToString("N")[..8];
        var r = await ana.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = marca, descripcion = "Con estuche", categoriaId = 1, modo = "Compra", condicion = "Usado",
            localidad = "Chapinero", precioReferenciaCop = 300000, aceptaTrueke = true
        });
        var venta = await CreadaAsync(r);
        Assert.True(venta.GetProperty("aceptaTrueke").GetBoolean());

        // En un trueke o una donación la casilla no aplica: se ignora
        var trueke = await Api.CrearPublicacionAsync(ana, aceptaTrueke: true);
        var detalle = await ana.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{trueke}");
        Assert.False(detalle.GetProperty("aceptaTrueke").GetBoolean());

        async Task<bool> EstaEnAsync(string modo) => (await _f.CreateClient()
                .GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones?texto={marca}&modo={modo}"))
            .GetProperty("items").EnumerateArray().Any(p => p.GetProperty("titulo").GetString() == marca);
        Assert.True(await EstaEnAsync("Trueke"));
        Assert.True(await EstaEnAsync("Compra"));
        Assert.False(await EstaEnAsync("Donacion"));
    }

    [Fact]
    public async Task A_una_venta_sin_la_casilla_no_se_le_ofrecen_objetos()
    {
        var ana = await Api.RegistrarAsync(_f, "anaVt2");
        var beto = await Api.RegistrarAsync(_f, "betoVt2");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra");
        var suyo = await Api.CrearPublicacionAsync(beto);

        var r = await SolicitarAsync(beto, venta, new { publicacionesOfrecidasIds = new[] { suyo } });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("no acepta trueke", await r.Content.ReadAsStringAsync());

        var s = await CreadaAsync(await SolicitarAsync(beto, venta));
        Assert.False(s.GetProperty("publicacionAceptaTrueke").GetBoolean());
        var contra = await ana.PostAsJsonAsync($"/api/v1/solicitudes/{s.GetProperty("id").GetGuid()}/contraoferta",
            new { publicacionesOfrecidasIds = new[] { suyo } });
        Assert.Equal(HttpStatusCode.BadRequest, contra.StatusCode);
    }

    [Fact]
    public async Task Si_se_cierra_como_trueke_suma_como_trueke_aunque_hayan_negociado_el_precio()
    {
        var ana = await Api.RegistrarAsync(_f, "anaVt3");
        var beto = await Api.RegistrarAsync(_f, "betoVt3");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra", aceptaTrueke: true);
        var b1 = await Api.CrearPublicacionAsync(beto);
        var b2 = await Api.CrearPublicacionAsync(beto);

        // Precio y objetos a la vez no vale: es una cosa o la otra
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, venta, new { publicacionesOfrecidasIds = new[] { b1 }, precioPropuestoCop = 10000m })).StatusCode);

        var s = await CreadaAsync(await SolicitarAsync(beto, venta, new { publicacionesOfrecidasIds = new[] { b1 } }));
        var id = s.GetProperty("id").GetGuid();
        Assert.Equal("Trueke", s.GetProperty("modo").GetString());
        Assert.True(s.GetProperty("publicacionAceptaTrueke").GetBoolean());
        Assert.Equal(50000m, s.GetProperty("precioPublicadoCop").GetDecimal());

        // Ana prefiere que le paguen; Beto vuelve a proponer trueke con otros objetos
        var tras = await ContraofertarAsync(ana, id, new { precioPropuestoCop = 45000m });
        Assert.Equal("Compra", tras.GetProperty("modo").GetString());
        Assert.Empty(tras.GetProperty("ofrecidas").EnumerateArray());
        Assert.Equal(45000m, tras.GetProperty("precioPropuestoCop").GetDecimal());
        tras = await ContraofertarAsync(beto, id, new { publicacionesOfrecidasIds = new[] { b1, b2 } });
        Assert.Equal("Trueke", tras.GetProperty("modo").GetString());
        Assert.Equal(JsonValueKind.Null, tras.GetProperty("precioPropuestoCop").ValueKind);

        (await ana.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        await CompletarAsync(ana, beto, id);
        Assert.Equal(10, await PuntosPorAsync(ana, id)); // Trueke, no Compra (5)
        Assert.Equal(10, await PuntosPorAsync(beto, id));
        var mias = await beto.GetFromJsonAsync<JsonElement[]>("/api/v1/publicaciones/mias");
        Assert.All(mias!.Where(p => p.GetProperty("id").GetGuid() == b1 || p.GetProperty("id").GetGuid() == b2),
            p => Assert.Equal("Intercambiada", p.GetProperty("estado").GetString()));
    }

    [Fact]
    public async Task Si_se_paga_el_precio_sigue_siendo_una_compra()
    {
        var ana = await Api.RegistrarAsync(_f, "anaVt4");
        var beto = await Api.RegistrarAsync(_f, "betoVt4");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra", aceptaTrueke: true);

        var s = await CreadaAsync(await SolicitarAsync(beto, venta));
        var id = s.GetProperty("id").GetGuid();
        Assert.Equal("Compra", s.GetProperty("modo").GetString());
        (await ana.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        await CompletarAsync(ana, beto, id);
        Assert.Equal(5, await PuntosPorAsync(ana, id));
    }
}
