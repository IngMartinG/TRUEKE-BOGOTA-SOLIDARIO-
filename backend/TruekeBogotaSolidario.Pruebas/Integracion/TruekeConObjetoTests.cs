using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Trueke con un objeto propio ofrecido a cambio: reservas, listas de espera y cierres en ambos lados.</summary>
public class TruekeConObjetoTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _f;
    public TruekeConObjetoTests(FabricaApi f) => _f = f;

    private static async Task<JsonElement> ProponerAsync(HttpClient c, Guid pedida, Guid? ofrecida, HttpStatusCode esperado = HttpStatusCode.Created)
    {
        var r = await c.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pedida, mensaje = "Te lo cambio", publicacionOfrecidaId = ofrecida });
        Assert.True(r.StatusCode == esperado, await r.Content.ReadAsStringAsync());
        return r.StatusCode == HttpStatusCode.Created ? await r.Content.ReadFromJsonAsync<JsonElement>() : default;
    }

    private static async Task<string> EstadoAsync(HttpClient duenio, Guid publicacion)
        => (await duenio.GetFromJsonAsync<JsonElement[]>("/api/v1/publicaciones/mias"))!
            .Single(p => p.GetProperty("id").GetGuid() == publicacion).GetProperty("estado").GetString()!;

    private static async Task<JsonElement> SolicitudAsync(HttpClient c, string bandeja, Guid id)
        => (await c.GetFromJsonAsync<JsonElement[]>($"/api/v1/solicitudes/{bandeja}"))!.Single(s => s.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task Al_aceptar_se_reservan_ambos_objetos_y_se_cierran_las_otras_ofertas_del_mismo_objeto()
    {
        var ana = await Api.RegistrarAsync(_f, "anaTk");
        var carla = await Api.RegistrarAsync(_f, "carlaTk");
        var beto = await Api.RegistrarAsync(_f, "betoTk");
        var diego = await Api.RegistrarAsync(_f, "diegoTk");
        var pubAna = await Api.CrearPublicacionAsync(ana);
        var pubCarla = await Api.CrearPublicacionAsync(carla);
        var pubBeto = await Api.CrearPublicacionAsync(beto);

        // Beto ofrece el mismo objeto en dos truekes; Diego pide el objeto de Beto
        var aAna = await ProponerAsync(beto, pubAna, pubBeto);
        Assert.Equal(pubBeto, aAna.GetProperty("ofrecidas")[0].GetProperty("id").GetGuid());
        var aCarla = (await ProponerAsync(beto, pubCarla, pubBeto)).GetProperty("id").GetGuid();
        var deDiego = (await ProponerAsync(diego, pubBeto, null)).GetProperty("id").GetGuid();

        // Ana ve lo que le ofrecen y acepta
        var recibida = await SolicitudAsync(ana, "recibidas", aAna.GetProperty("id").GetGuid());
        Assert.Equal("Bicicleta usada", recibida.GetProperty("ofrecidas")[0].GetProperty("titulo").GetString());
        (await ana.PostAsync($"/api/v1/solicitudes/{aAna.GetProperty("id").GetGuid()}/aceptar", null)).EnsureSuccessStatusCode();

        Assert.Equal("EnNegociacion", await EstadoAsync(ana, pubAna));
        Assert.Equal("EnNegociacion", await EstadoAsync(beto, pubBeto)); // el objeto ofrecido también quedó reservado
        Assert.Equal(HttpStatusCode.NotFound, (await _f.CreateClient().GetAsync($"/api/v1/publicaciones/{pubBeto}")).StatusCode); // fuera del catálogo
        Assert.Equal(HttpStatusCode.OK, (await ana.GetAsync($"/api/v1/publicaciones/{pubBeto}")).StatusCode);   // Ana ve lo que va a recibir
        Assert.Equal(HttpStatusCode.NotFound, (await diego.GetAsync($"/api/v1/publicaciones/{pubBeto}")).StatusCode); // un tercero no

        var cancelada = await SolicitudAsync(carla, "recibidas", aCarla);
        Assert.Equal("Cancelada", cancelada.GetProperty("estado").GetString());
        Assert.True((await SolicitudAsync(diego, "enviadas", deDiego)).GetProperty("enEspera").GetBoolean());
    }

    [Fact]
    public async Task Completado_intercambia_los_dos_objetos_y_no_concretado_los_devuelve_al_catalogo()
    {
        var ana = await Api.RegistrarAsync(_f, "anaTk2");
        var beto = await Api.RegistrarAsync(_f, "betoTk2");
        var diego = await Api.RegistrarAsync(_f, "diegoTk2");

        // No concretado: los dos vuelven y la lista de espera del objeto ofrecido sigue en pie
        var pubAna = await Api.CrearPublicacionAsync(ana);
        var pubBeto = await Api.CrearPublicacionAsync(beto);
        var s1 = (await ProponerAsync(beto, pubAna, pubBeto)).GetProperty("id").GetGuid();
        var deDiego = (await ProponerAsync(diego, pubBeto, null)).GetProperty("id").GetGuid();
        (await ana.PostAsync($"/api/v1/solicitudes/{s1}/aceptar", null)).EnsureSuccessStatusCode();
        (await beto.PostAsJsonAsync($"/api/v1/solicitudes/{s1}/no-concretada", new { motivo = "No pudimos vernos" })).EnsureSuccessStatusCode();
        Assert.Equal("Disponible", await EstadoAsync(ana, pubAna));
        Assert.Equal("Disponible", await EstadoAsync(beto, pubBeto));
        Assert.False((await SolicitudAsync(diego, "enviadas", deDiego)).GetProperty("enEspera").GetBoolean());

        // Completado: los dos quedan intercambiados y la lista de espera del objeto ofrecido se cierra
        var s2 = (await ProponerAsync(beto, pubAna, pubBeto)).GetProperty("id").GetGuid();
        (await ana.PostAsync($"/api/v1/solicitudes/{s2}/aceptar", null)).EnsureSuccessStatusCode();
        (await ana.PostAsync($"/api/v1/solicitudes/{s2}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await beto.PostAsync($"/api/v1/solicitudes/{s2}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        Assert.Equal("Intercambiada", await EstadoAsync(ana, pubAna));
        Assert.Equal("Intercambiada", await EstadoAsync(beto, pubBeto));
        Assert.Equal("Rechazada", (await SolicitudAsync(diego, "enviadas", deDiego)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task Solo_se_ofrece_un_objeto_propio_disponible_y_solo_en_un_trueke()
    {
        var ana = await Api.RegistrarAsync(_f, "anaTk3");
        var beto = await Api.RegistrarAsync(_f, "betoTk3");
        var pubAna = await Api.CrearPublicacionAsync(ana);
        var ventaAna = await Api.CrearPublicacionAsync(ana, modo: "Compra");
        var pubBeto = await Api.CrearPublicacionAsync(beto);

        await ProponerAsync(beto, pubAna, pubAna, HttpStatusCode.NotFound);         // un objeto ajeno
        await ProponerAsync(beto, ventaAna, pubBeto, HttpStatusCode.BadRequest);    // en una compra no se ofrece objeto
        await ProponerAsync(beto, pubAna, Guid.NewGuid(), HttpStatusCode.NotFound); // no existe

        // Mientras está ofrecido no se edita; si lo retira, la propuesta se cierra sola
        var s = (await ProponerAsync(beto, pubAna, pubBeto)).GetProperty("id").GetGuid();
        var editar = await beto.PutAsJsonAsync($"/api/v1/publicaciones/{pubBeto}", new
        {
            titulo = "Otra cosa", descripcion = "Cambiado", categoriaId = 1, modo = "Trueke", condicion = "Usado", localidad = "Chapinero"
        });
        Assert.Equal(HttpStatusCode.BadRequest, editar.StatusCode);
        (await beto.PostAsJsonAsync($"/api/v1/publicaciones/{pubBeto}/cancelar", new { motivo = "Ya no" })).EnsureSuccessStatusCode();
        var cerrada = await SolicitudAsync(ana, "recibidas", s);
        Assert.Equal("Cancelada", cerrada.GetProperty("estado").GetString());
        Assert.Contains("retirado por su dueño", cerrada.GetProperty("motivoRechazo").GetString());
    }

    [Fact]
    public async Task Si_el_objeto_ofrecido_ya_no_esta_disponible_no_se_puede_aceptar()
    {
        var ana = await Api.RegistrarAsync(_f, "anaTk4");
        var beto = await Api.RegistrarAsync(_f, "betoTk4");
        var diego = await Api.RegistrarAsync(_f, "diegoTk4");
        var pubAna = await Api.CrearPublicacionAsync(ana);
        var pubBeto = await Api.CrearPublicacionAsync(beto);
        var oferta = (await ProponerAsync(beto, pubAna, pubBeto)).GetProperty("id").GetGuid();

        // Beto le entrega su objeto a Diego (lo reserva como publicación propia): la oferta a Ana se cierra sola
        var deDiego = (await ProponerAsync(diego, pubBeto, null)).GetProperty("id").GetGuid();
        (await beto.PostAsync($"/api/v1/solicitudes/{deDiego}/aceptar", null)).EnsureSuccessStatusCode();
        Assert.Equal("Cancelada", (await SolicitudAsync(ana, "recibidas", oferta)).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsync($"/api/v1/solicitudes/{oferta}/aceptar", null)).StatusCode);
    }
}
