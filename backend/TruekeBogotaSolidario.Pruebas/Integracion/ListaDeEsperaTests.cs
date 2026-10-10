using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Varias personas pueden interesarse a la vez: la publicación sigue en el catálogo hasta que el dueño elige a una.
/// Las demás quedan en lista de espera; si el intercambio no se concreta vuelve al catálogo, y si se completa se cierra la lista.
/// </summary>
public class ListaDeEsperaTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ListaDeEsperaTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<(HttpClient Duenio, HttpClient A, HttpClient B, Guid Pub, Guid SolA, Guid SolB)> DosInteresadosAsync(string sufijo)
    {
        var duenio = await Api.RegistrarAsync(_fabrica, $"duenio{sufijo}");
        var a = await Api.RegistrarAsync(_fabrica, $"interesa{sufijo}");
        var b = await Api.RegistrarAsync(_fabrica, $"interesb{sufijo}");
        var pub = await Api.CrearPublicacionAsync(duenio);
        return (duenio, a, b, pub, await SolicitarAsync(a, pub), await SolicitarAsync(b, pub));
    }

    private static async Task<Guid> SolicitarAsync(HttpClient c, Guid pub)
    {
        var r = await c.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa mucho" });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> MiSolicitudAsync(HttpClient c, Guid solicitud)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/solicitudes/enviadas")).EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == solicitud);

    [Fact]
    public async Task Varias_personas_pueden_solicitar_y_la_publicacion_sigue_en_el_catalogo()
    {
        var (_, a, _, pub, _, _) = await DosInteresadosAsync("v");

        var publica = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}");
        Assert.Equal("Disponible", publica.GetProperty("estado").GetString());
        Assert.Equal(2, publica.GetProperty("interesados").GetInt32());
        Assert.False(publica.GetProperty("yaSolicite").GetBoolean());

        Assert.True((await a.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}")).GetProperty("yaSolicite").GetBoolean());
        // la misma persona no puede enviar dos solicitudes para la misma publicación
        Assert.Equal(HttpStatusCode.BadRequest,
            (await a.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Otra vez" })).StatusCode);
    }

    [Fact]
    public async Task Al_aceptar_a_una_se_reserva_y_las_demas_quedan_en_lista_de_espera()
    {
        var (duenio, _, b, pub, solA, solB) = await DosInteresadosAsync("a");
        (await duenio.PostAsync($"/api/v1/solicitudes/{solA}/aceptar", null)).EnsureSuccessStatusCode();

        // sale del catálogo (para quien no participa no existe)
        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);
        var enEspera = await MiSolicitudAsync(b, solB);
        Assert.Equal("Pendiente", enEspera.GetProperty("estado").GetString());
        Assert.True(enEspera.GetProperty("enEspera").GetBoolean());

        // no se puede elegir a una segunda persona mientras la primera está concretando
        Assert.Equal(HttpStatusCode.BadRequest, (await duenio.PostAsync($"/api/v1/solicitudes/{solB}/aceptar", null)).StatusCode);
        var tercera = await Api.RegistrarAsync(_fabrica, "terceraa");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await tercera.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Sigue?" })).StatusCode);
    }

    [Fact]
    public async Task Si_no_se_concreta_vuelve_al_catalogo_y_el_duenio_puede_elegir_de_la_lista()
    {
        var (duenio, a, b, pub, solA, solB) = await DosInteresadosAsync("n");
        (await duenio.PostAsync($"/api/v1/solicitudes/{solA}/aceptar", null)).EnsureSuccessStatusCode();
        (await a.PostAsJsonAsync($"/api/v1/solicitudes/{solA}/no-concretada", new { motivo = "No pude ir" })).EnsureSuccessStatusCode();

        var publica = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}");
        Assert.Equal("Disponible", publica.GetProperty("estado").GetString());
        Assert.Equal(1, publica.GetProperty("interesados").GetInt32()); // B sigue interesada
        Assert.False((await MiSolicitudAsync(b, solB)).GetProperty("enEspera").GetBoolean());

        (await duenio.PostAsync($"/api/v1/solicitudes/{solB}/aceptar", null)).EnsureSuccessStatusCode();
        Assert.Equal("Aceptada", (await MiSolicitudAsync(b, solB)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Al_completar_se_cierra_la_lista_de_espera_y_la_publicacion_queda_intercambiada()
    {
        var (duenio, a, b, pub, solA, solB) = await DosInteresadosAsync("c");
        (await duenio.PostAsync($"/api/v1/solicitudes/{solA}/aceptar", null)).EnsureSuccessStatusCode();
        (await duenio.PostAsync($"/api/v1/solicitudes/{solA}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await a.PostAsync($"/api/v1/solicitudes/{solA}/confirmar-entrega", null)).EnsureSuccessStatusCode();

        Assert.Equal("Completada", (await MiSolicitudAsync(a, solA)).GetProperty("estado").GetString());
        var cerrada = await MiSolicitudAsync(b, solB);
        Assert.Equal("Rechazada", cerrada.GetProperty("estado").GetString());
        Assert.Contains("otra persona", cerrada.GetProperty("motivoRechazo").GetString());
        var mia = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}");
        Assert.Equal("Intercambiada", mia.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Rechazar_o_cancelar_una_solicitud_no_saca_la_publicacion_del_catalogo()
    {
        var (duenio, _, b, pub, solA, solB) = await DosInteresadosAsync("r");
        (await duenio.PostAsJsonAsync($"/api/v1/solicitudes/{solA}/rechazar", new { motivo = "Prefiero otra oferta" })).EnsureSuccessStatusCode();
        (await b.PostAsync($"/api/v1/solicitudes/{solB}/cancelar", null)).EnsureSuccessStatusCode();

        var publica = await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}");
        Assert.Equal("Disponible", publica.GetProperty("estado").GetString());
        Assert.Equal(0, publica.GetProperty("interesados").GetInt32());
    }

    [Fact]
    public async Task Cancelar_la_publicacion_avisa_a_todas_las_personas_interesadas()
    {
        var (duenio, a, b, pub, solA, solB) = await DosInteresadosAsync("x");
        (await duenio.PostAsJsonAsync($"/api/v1/publicaciones/{pub}/cancelar", new { motivo = "Ya no la vendo" })).EnsureSuccessStatusCode();
        Assert.Equal("Rechazada", (await MiSolicitudAsync(a, solA)).GetProperty("estado").GetString());
        Assert.Equal("Rechazada", (await MiSolicitudAsync(b, solB)).GetProperty("estado").GetString());
    }
}
