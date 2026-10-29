using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Revisión previa por riesgo: las publicaciones de riesgo (aquí, las primeras de una cuenta nueva) esperan a un
/// moderador y no se ven en el catálogo; al aprobarlas entran; al rechazarlas el dueño corrige y vuelven a la fila.
/// </summary>
public sealed class ModeracionPublicacionesTests : IClassFixture<ModeracionPublicacionesTests.FabricaConModeracion>
{
    private readonly FabricaConModeracion _f;
    public ModeracionPublicacionesTests(FabricaConModeracion f) => _f = f;

    public sealed class FabricaConModeracion : FabricaApi
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Moderacion:PreAprobacion", "true");
        }
    }

    private async Task<HttpClient> ModeradorAsync() => Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private static async Task<Guid> PublicarAsync(HttpClient c, string titulo, string descripcion = "En buen estado")
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones",
            new { titulo, descripcion, categoriaId = 4, modo = "Trueke", condicion = "Usado", localidad = "Kennedy" });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<bool> EnCatalogoAsync(string titulo)
        => (await _f.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones?texto={Uri.EscapeDataString(titulo)}"))
            .GetProperty("items").EnumerateArray().Any(p => p.GetProperty("titulo").GetString() == titulo);

    private static async Task<JsonElement?> EnFilaAsync(HttpClient mod, Guid id)
        => (await mod.GetFromJsonAsync<JsonElement>("/api/v1/admin/moderacion/publicaciones")).GetProperty("items").EnumerateArray()
            .Cast<JsonElement?>().FirstOrDefault(p => p!.Value.GetProperty("id").GetGuid() == id);

    private static async Task<string[]> TiposAvisosAsync(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("tipo").GetString()!).ToArray();

    [Fact]
    public async Task La_primera_publicacion_de_una_cuenta_nueva_espera_revision_y_al_aprobarla_entra_al_catalogo()
    {
        var duenio = await Api.RegistrarAsync(_f, "primeriza");
        var titulo = "Lampara " + Guid.NewGuid().ToString("N")[..8];
        var id = await PublicarAsync(duenio, titulo);

        Assert.False(await EnCatalogoAsync(titulo));
        // Nadie más la ve; el dueño sí, con su estado.
        var otro = await Api.RegistrarAsync(_f, "curioso");
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/v1/publicaciones/{id}")).StatusCode);
        var propia = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{id}");
        Assert.Equal("Pendiente", propia.GetProperty("moderacion").GetString());
        Assert.Contains("PublicacionEnRevision", await TiposAvisosAsync(duenio));
        // No se puede pedir ni impulsar lo que aún no está en el catálogo.
        var solicitud = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = id, mensaje = "Me interesa" });
        Assert.Equal(HttpStatusCode.NotFound, solicitud.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await duenio.PostAsync($"/api/v1/publicaciones/{id}/impulsar", null)).StatusCode);

        var mod = await ModeradorAsync();
        var item = await EnFilaAsync(mod, id);
        Assert.NotNull(item);
        Assert.Contains(item!.Value.GetProperty("motivos").EnumerateArray(), m => m.GetString()!.Contains("primeras 3"));
        Assert.Equal("Kennedy", item.Value.GetProperty("localidad").GetString());
        Assert.True((await mod.GetFromJsonAsync<JsonElement>("/api/v1/admin/moderacion/cola")).GetProperty("publicaciones").GetInt32() >= 1);

        var r = await mod.PostAsJsonAsync("/api/v1/admin/moderacion/publicaciones/aprobar", new { ids = new[] { id, Guid.NewGuid() } });
        Assert.Equal(1, await r.Content.ReadFromJsonAsync<int>()); // el id inexistente se ignora
        Assert.True(await EnCatalogoAsync(titulo));
        Assert.Null(await EnFilaAsync(mod, id));
        Assert.Contains("PublicacionAprobada", await TiposAvisosAsync(duenio));
        // Aprobarla otra vez no hace nada (otro moderador ya la resolvió).
        r = await mod.PostAsJsonAsync("/api/v1/admin/moderacion/publicaciones/aprobar", new { ids = new[] { id } });
        Assert.Equal(0, await r.Content.ReadFromJsonAsync<int>());
    }

    [Fact]
    public async Task Rechazada_el_duenio_ve_el_motivo_y_al_corregirla_vuelve_a_la_fila()
    {
        var duenio = await Api.RegistrarAsync(_f, "corrige");
        var titulo = "Cachorros " + Guid.NewGuid().ToString("N")[..8];
        var id = await PublicarAsync(duenio, titulo, "Escríbeme al 310 555 1234");
        var mod = await ModeradorAsync();
        var item = (await EnFilaAsync(mod, id))!.Value;
        var motivos = item.GetProperty("motivos").EnumerateArray().Select(m => m.GetString()).ToList();
        Assert.Contains("Datos de contacto o enlaces en el texto", motivos);
        Assert.Contains(motivos, m => m!.StartsWith("Posible artículo prohibido"));

        var r = await mod.PostAsJsonAsync($"/api/v1/admin/moderacion/publicaciones/{id}/rechazar", new { motivo = "No se permiten animales vivos." });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        var propia = await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{id}");
        Assert.Equal("Rechazada", propia.GetProperty("moderacion").GetString());
        Assert.Equal("No se permiten animales vivos.", propia.GetProperty("motivoRechazoModeracion").GetString());
        Assert.Contains("PublicacionRechazada", await TiposAvisosAsync(duenio));
        Assert.Null(await EnFilaAsync(mod, id));

        var editar = await duenio.PutAsJsonAsync($"/api/v1/publicaciones/{id}",
            new { titulo = "Casa para perro", descripcion = "De madera", categoriaId = 4, modo = "Trueke", condicion = "Usado", localidad = "Kennedy" });
        Assert.Equal(HttpStatusCode.OK, editar.StatusCode);
        Assert.Equal("Pendiente", (await editar.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("moderacion").GetString());
        var otraVez = (await EnFilaAsync(mod, id))!.Value;
        Assert.Contains(otraVez.GetProperty("motivos").EnumerateArray(), m => m.GetString() == "Corregida tras un rechazo");
    }

    [Fact]
    public async Task Tres_personas_distintas_denunciando_la_sacan_del_catalogo_hasta_revisarla()
    {
        var duenio = await Api.RegistrarAsync(_f, "denunciado");
        var titulo = "Bicicleta " + Guid.NewGuid().ToString("N")[..8];
        var id = await PublicarAsync(duenio, titulo);
        var mod = await ModeradorAsync();
        await mod.PostAsJsonAsync("/api/v1/admin/moderacion/publicaciones/aprobar", new { ids = new[] { id } });
        Assert.True(await EnCatalogoAsync(titulo));

        for (var i = 0; i < 3; i++)
        {
            Assert.True(await EnCatalogoAsync(titulo), $"Con {i} denuncias debe seguir visible");
            var vecino = await Api.RegistrarAsync(_f, "vecino" + i);
            var r = await vecino.PostAsJsonAsync("/api/v1/denuncias", new { tipo = "Publicacion", objetivoId = id, motivo = "Fraude" });
            Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        }

        Assert.False(await EnCatalogoAsync(titulo));
        var item = (await EnFilaAsync(mod, id))!.Value;
        Assert.Contains(item.GetProperty("motivos").EnumerateArray(), m => m.GetString() == "Denunciada por 3 personas");
    }

    [Fact]
    public async Task Solo_moderadores_ven_la_fila_y_nadie_revisa_lo_propio()
    {
        var cliente = await Api.RegistrarAsync(_f, "noModera");
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/v1/admin/moderacion/publicaciones")).StatusCode);

        // Lo que publica un moderador no espera a un colega.
        var mod = await ModeradorAsync();
        await Api.PonerFotoAsync(_f, (await mod.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid());
        var titulo = "Mesa " + Guid.NewGuid().ToString("N")[..8];
        await PublicarAsync(mod, titulo);
        Assert.True(await EnCatalogoAsync(titulo));
    }
}
