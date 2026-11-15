using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Trueke con complemento en dinero: objetos + un monto en pesos que las partes se pagan entre ellas (la plataforma no lo
/// mueve). Lo puede proponer cualquiera de las dos partes, indica quién le paga a quién y cuenta como trueke.
/// </summary>
public class TruekeConComplementoTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public TruekeConComplementoTests(FabricaApi f) => _f = f;

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

    private static Task<HttpResponseMessage> ContraofertarAsync(HttpClient c, Guid solicitud, object cuerpo)
        => c.PostAsJsonAsync($"/api/v1/solicitudes/{solicitud}/contraoferta", cuerpo);

    private static async Task<JsonElement> OkAsync(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<int> PuntosPorAsync(HttpClient c, Guid solicitud)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/eco-puntos/movimientos?pagina=1&tamano=50"))
            .GetProperty("items").EnumerateArray()
            .Single(m => m.GetProperty("concepto").GetString() == "Intercambio" && m.GetProperty("referenciaId").GetGuid() == solicitud)
            .GetProperty("puntos").GetInt32();

    private static async Task<string[]> AvisosAsync(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones?pagina=1&tamano=20")).GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("mensaje").GetString()!).ToArray();

    [Fact]
    public async Task El_dinero_exige_objetos_y_quien_paga_y_un_monto_entero_valido()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCc1");
        var beto = await Api.RegistrarAsync(_f, "betoCc1");
        var pedida = await Api.CrearPublicacionAsync(ana);
        var suyo = await Api.CrearPublicacionAsync(beto);

        // Sin objetos sería una compra
        var sinObjetos = await SolicitarAsync(beto, pedida, new { complementoCop = 50000m, complementoLoPagaSolicitante = true });
        Assert.Equal(HttpStatusCode.BadRequest, sinObjetos.StatusCode);
        Assert.Contains("al menos un objeto", await sinObjetos.Content.ReadAsStringAsync());
        // El sentido es obligatorio cuando hay monto
        var sinSentido = await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 50000m });
        Assert.Equal(HttpStatusCode.BadRequest, sinSentido.StatusCode);
        Assert.Contains("le paga a", await sinSentido.Content.ReadAsStringAsync());
        // Sentido sin monto, monto cero, con centavos o por encima del tope
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo }, complementoLoPagaSolicitante = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 0m, complementoLoPagaSolicitante = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 1500.5m, complementoLoPagaSolicitante = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 1_000_000_001m, complementoLoPagaSolicitante = true })).StatusCode);
        // Precio y dinero junto a objetos tampoco
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo }, precioPropuestoCop = 1000m, complementoCop = 5000m, complementoLoPagaSolicitante = true })).StatusCode);

        // Y una propuesta de objetos sin dinero sigue como siempre
        var s = await CreadaAsync(await SolicitarAsync(beto, pedida, new { publicacionesOfrecidasIds = new[] { suyo } }));
        Assert.Equal(JsonValueKind.Null, s.GetProperty("complementoCop").ValueKind);
        Assert.Equal(JsonValueKind.Null, s.GetProperty("complementoLoPagaSolicitante").ValueKind);
    }

    [Fact]
    public async Task Cualquiera_de_las_partes_propone_el_dinero_y_completar_cuenta_como_trueke()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCc2");
        var beto = await Api.RegistrarAsync(_f, "betoCc2");
        var pedida = await Api.CrearPublicacionAsync(ana);
        var b1 = await Api.CrearPublicacionAsync(beto);
        var b2 = await Api.CrearPublicacionAsync(beto);

        // Beto ofrece un objeto más económico y paga la diferencia
        var s = await CreadaAsync(await SolicitarAsync(beto, pedida,
            new { publicacionesOfrecidasIds = new[] { b1 }, complementoCop = 50000m, complementoLoPagaSolicitante = true }));
        var id = s.GetProperty("id").GetGuid();
        Assert.Equal("Trueke", s.GetProperty("modo").GetString());
        Assert.Equal(50000m, s.GetProperty("complementoCop").GetDecimal());
        Assert.True(s.GetProperty("complementoLoPagaSolicitante").GetBoolean());
        Assert.Contains(await AvisosAsync(ana), a => a.Contains("+ $50.000 de su parte"));

        // La misma propuesta (mismos objetos y monto) no es una contraoferta; con otro monto sí
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ContraofertarAsync(ana, id, new { publicacionesOfrecidasIds = new[] { b1 }, complementoCop = 50000m, complementoLoPagaSolicitante = true })).StatusCode);
        // Ana (dueña) pide más dinero; Beto responde con otro objeto sin dinero; Ana vuelve con dinero que paga ella
        var tras = await OkAsync(await ContraofertarAsync(ana, id,
            new { publicacionesOfrecidasIds = new[] { b1 }, complementoCop = 80000m, complementoLoPagaSolicitante = true }));
        Assert.Equal(80000m, tras.GetProperty("complementoCop").GetDecimal());
        Assert.Contains(await AvisosAsync(beto), a => a.Contains("+ $80.000 de tu parte"));
        tras = await OkAsync(await ContraofertarAsync(beto, id, new { publicacionesOfrecidasIds = new[] { b1, b2 } }));
        Assert.Equal(JsonValueKind.Null, tras.GetProperty("complementoCop").ValueKind);
        tras = await OkAsync(await ContraofertarAsync(ana, id,
            new { publicacionesOfrecidasIds = new[] { b1, b2 }, complementoCop = 20000m, complementoLoPagaSolicitante = false }));
        Assert.False(tras.GetProperty("complementoLoPagaSolicitante").GetBoolean());
        Assert.Contains(await AvisosAsync(beto), a => a.Contains("+ $20.000 de su parte"));

        var aceptada = await OkAsync(await beto.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null));
        Assert.Equal(20000m, aceptada.GetProperty("complementoCop").GetDecimal());
        (await ana.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await beto.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        // Es un trueke: 10 Eco-Puntos para cada uno, sin puntos extra por el dinero
        Assert.Equal(10, await PuntosPorAsync(ana, id));
        Assert.Equal(10, await PuntosPorAsync(beto, id));
        var mias = await beto.GetFromJsonAsync<JsonElement[]>("/api/v1/publicaciones/mias");
        Assert.All(mias!.Where(p => p.GetProperty("id").GetGuid() == b1 || p.GetProperty("id").GetGuid() == b2),
            p => Assert.Equal("Intercambiada", p.GetProperty("estado").GetString()));
    }

    [Fact]
    public async Task Una_donacion_y_una_compra_no_admiten_dinero_sumado_a_objetos()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCc3");
        var beto = await Api.RegistrarAsync(_f, "betoCc3");
        var donacion = await Api.CrearPublicacionAsync(ana, modo: "Donacion");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra");
        var suyo = await Api.CrearPublicacionAsync(beto);
        var dinero = new { complementoCop = 5000m, complementoLoPagaSolicitante = true };

        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(beto, donacion, dinero)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, donacion, new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 5000m, complementoLoPagaSolicitante = true })).StatusCode);
        // Una venta que no acepta trueke: ni objetos ni dinero sumado
        Assert.Equal(HttpStatusCode.BadRequest, (await SolicitarAsync(beto, venta, dinero)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, venta, new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 5000m, complementoLoPagaSolicitante = true })).StatusCode);

        // En la compra el dueño tampoco puede contraofertar con dinero sumado
        var s = await CreadaAsync(await SolicitarAsync(beto, venta));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ContraofertarAsync(ana, s.GetProperty("id").GetGuid(), dinero)).StatusCode);
    }

    [Fact]
    public async Task En_una_venta_que_acepta_trueke_el_dinero_solo_vale_cuando_el_modo_queda_en_trueke()
    {
        var ana = await Api.RegistrarAsync(_f, "anaCc4");
        var beto = await Api.RegistrarAsync(_f, "betoCc4");
        var venta = await Api.CrearPublicacionAsync(ana, modo: "Compra", aceptaTrueke: true);
        var suyo = await Api.CrearPublicacionAsync(beto);

        // Pagar y a la vez sumar dinero a objetos no vale; sin objetos, tampoco
        Assert.Equal(HttpStatusCode.BadRequest,
            (await SolicitarAsync(beto, venta, new { precioPropuestoCop = 40000m, complementoCop = 5000m, complementoLoPagaSolicitante = true })).StatusCode);
        var s = await CreadaAsync(await SolicitarAsync(beto, venta, new { precioPropuestoCop = 40000m }));
        var id = s.GetProperty("id").GetGuid();
        Assert.Equal("Compra", s.GetProperty("modo").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ContraofertarAsync(ana, id, new { complementoCop = 5000m, complementoLoPagaSolicitante = true })).StatusCode);

        // La contraoferta pasa de precio a objetos + dinero: el modo queda en Trueke
        var tras = await OkAsync(await ContraofertarAsync(ana, id,
            new { publicacionesOfrecidasIds = new[] { suyo }, complementoCop = 30000m, complementoLoPagaSolicitante = true }));
        Assert.Equal("Trueke", tras.GetProperty("modo").GetString());
        Assert.Equal(30000m, tras.GetProperty("complementoCop").GetDecimal());
        Assert.Equal(JsonValueKind.Null, tras.GetProperty("precioPropuestoCop").ValueKind);

        // Y si vuelven a proponer un precio, el dinero sumado desaparece
        tras = await OkAsync(await ContraofertarAsync(beto, id, new { precioPropuestoCop = 45000m }));
        Assert.Equal("Compra", tras.GetProperty("modo").GetString());
        Assert.Equal(JsonValueKind.Null, tras.GetProperty("complementoCop").ValueKind);
        Assert.Equal(JsonValueKind.Null, tras.GetProperty("complementoLoPagaSolicitante").ValueKind);
    }
}
