using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class ComentariosTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ComentariosTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<HttpClient> SuperAsync()
        => Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private static async Task<List<JsonElement>> ItemsAsync(HttpClient c, Guid pub)
    {
        var r = await c.GetAsync($"/api/v1/publicaciones/{pub}/comentarios");
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
    }

    [Fact]
    public async Task Cliente_comenta_moderador_oculta_y_el_publico_deja_de_verlo()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "vendedora");
        var cliente = await Api.RegistrarAsync(_fabrica, "comprador");
        var anonimo = _fabrica.CreateClient();
        var pub = await Api.CrearPublicacionAsync(duenio);

        var crear = await cliente.PostAsJsonAsync($"/api/v1/publicaciones/{pub}/comentarios", new { texto = "¿Sigue disponible?" });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var comentarioId = (await crear.Content.ReadFromJsonAsync<IdDto>())!.Id;

        var visibles = await ItemsAsync(anonimo, pub);
        var c = Assert.Single(visibles);
        Assert.Equal("¿Sigue disponible?", c.GetProperty("texto").GetString());
        Assert.False(c.TryGetProperty("autorId", out _));                  // no se expone el id del autor
        Assert.Equal("comprador P.", c.GetProperty("autor").GetProperty("nombre").GetString());

        // un Cliente no puede moderar
        Assert.Equal(HttpStatusCode.Forbidden,
            (await cliente.PostAsJsonAsync($"/api/v1/admin/comentarios/{comentarioId}/ocultar", new { motivo = "spam spam" })).StatusCode);

        var super = await SuperAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await super.PostAsJsonAsync($"/api/v1/admin/comentarios/{comentarioId}/ocultar", new { motivo = "Lenguaje ofensivo" })).StatusCode);

        Assert.Empty(await ItemsAsync(anonimo, pub));
        Assert.Empty(await ItemsAsync(cliente, pub));                       // ni siquiera su autor lo ve
        var paraModerador = Assert.Single(await ItemsAsync(super, pub));
        Assert.True(paraModerador.GetProperty("oculto").GetBoolean());
        Assert.Equal("Lenguaje ofensivo", paraModerador.GetProperty("motivoOcultamiento").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await super.PostAsync($"/api/v1/admin/comentarios/{comentarioId}/mostrar", null)).StatusCode);
        Assert.Single(await ItemsAsync(anonimo, pub));
    }

    [Fact]
    public async Task Anonimo_no_puede_comentar()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "x");
        var pub = await Api.CrearPublicacionAsync(duenio);
        var r = await _fabrica.CreateClient().PostAsJsonAsync($"/api/v1/publicaciones/{pub}/comentarios", new { texto = "hola" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Comentario_vacio_o_largo_es_400_y_publicacion_inexistente_404()
    {
        var cliente = await Api.RegistrarAsync(_fabrica, "y");
        var pub = await Api.CrearPublicacionAsync(cliente);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync($"/api/v1/publicaciones/{pub}/comentarios", new { texto = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsJsonAsync($"/api/v1/publicaciones/{pub}/comentarios", new { texto = new string('a', 501) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.PostAsJsonAsync($"/api/v1/publicaciones/{Guid.NewGuid()}/comentarios", new { texto = "hola" })).StatusCode);
    }

    [Fact]
    public async Task Publicacion_oculta_no_expone_comentarios_al_publico()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "z");
        var pub = await Api.CrearPublicacionAsync(duenio);
        var super = await SuperAsync();
        (await super.PostAsJsonAsync($"/api/v1/admin/publicaciones/{pub}/ocultar", new { motivo = "Contenido prohibido" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await _fabrica.CreateClient().GetAsync($"/api/v1/publicaciones/{pub}/comentarios")).StatusCode);
    }
}
