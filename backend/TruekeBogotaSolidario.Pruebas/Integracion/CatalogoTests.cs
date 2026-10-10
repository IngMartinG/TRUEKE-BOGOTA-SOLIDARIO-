using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Perfil público, edición, varias fotos, favoritos, orden y filtros.</summary>
public class CatalogoTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public CatalogoTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static object Cuerpo(string titulo = "Mesa de noche", string modo = "Trueke", decimal? precio = null, string[]? imagenes = null)
        => new { titulo, descripcion = "Madera", categoriaId = 4, modo, condicion = "Usado", localidad = "Kennedy", precioReferenciaCop = precio, imagenes };

    private static async Task<Guid> CrearAsync(HttpClient c, object cuerpo)
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", cuerpo);
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }

    [Fact]
    public async Task Perfil_publico_sin_datos_privados_y_con_sus_publicaciones()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "perfilada");
        var c = Api.ConToken(_fabrica, ses.Token);
        var pub = await CrearAsync(c, Cuerpo());

        var anonimo = _fabrica.CreateClient();
        var detalle = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}");
        var idPropietario = detalle.GetProperty("propietario").GetProperty("id").GetGuid();
        Assert.Equal(ses.Usuario.Id, idPropietario);

        var r = await anonimo.GetAsync($"/api/v1/usuarios/{idPropietario}/perfil");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cuerpo = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("@", cuerpo);                         // sin correo
        Assert.DoesNotContain("perfilada Prueba", cuerpo);          // sin nombre completo
        var perfil = JsonDocument.Parse(cuerpo).RootElement;
        Assert.Equal("perfilada P.", perfil.GetProperty("nombre").GetString());
        Assert.Equal(1, perfil.GetProperty("publicacionesActivas").GetInt32());
        Assert.EndsWith("-01T00:00:00.000Z", perfil.GetProperty("miembroDesde").GetString()); // solo mes y año

        var pubs = await anonimo.GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{idPropietario}/publicaciones");
        Assert.Equal(1, pubs.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await anonimo.GetAsync($"/api/v1/usuarios/{Guid.NewGuid()}/perfil")).StatusCode);
    }

    [Fact]
    public async Task Editar_solo_el_duenio_y_solo_si_esta_disponible()
    {
        var c = await Api.RegistrarAsync(_fabrica, "editora");
        var pub = await CrearAsync(c, Cuerpo());
        var r = await c.PutAsJsonAsync($"/api/v1/publicaciones/{pub}", Cuerpo("Mesa de noche restaurada", "Compra", 80000));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var editada = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Mesa de noche restaurada", editada.GetProperty("titulo").GetString());
        Assert.Equal(80000, editada.GetProperty("precioReferenciaCop").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, editada.GetProperty("fechaEdicion").ValueKind);

        var otro = await Api.RegistrarAsync(_fabrica, "intrusa");
        Assert.Equal(HttpStatusCode.NotFound, (await otro.PutAsJsonAsync($"/api/v1/publicaciones/{pub}", Cuerpo("Hackeada"))).StatusCode);

        // con una solicitud en curso ya no se puede cambiar lo acordado
        (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "La quiero" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/v1/publicaciones/{pub}", Cuerpo("Otro título"))).StatusCode);
    }

    [Fact]
    public async Task Maximo_5_fotos_y_sin_repetidas()
    {
        var c = await Api.RegistrarAsync(_fabrica, "fotos");
        var seis = Enumerable.Range(1, 6).Select(i => $"https://imagenes.example/{i}.jpg").ToArray();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/publicaciones", Cuerpo(imagenes: seis))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/v1/publicaciones",
            Cuerpo(imagenes: new[] { "https://imagenes.example/a.jpg", "https://imagenes.example/a.jpg" }))).StatusCode);
        var id = await CrearAsync(c, Cuerpo(imagenes: seis[..3]));
        var p = await c.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{id}");
        Assert.Equal(seis[..3], p.GetProperty("imagenes").EnumerateArray().Select(x => x.GetString()).ToArray()); // orden conservado
    }

    [Fact]
    public async Task Favoritos_idempotentes_marcados_en_el_catalogo_y_listables()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "vende");
        var pub = await CrearAsync(duenio, Cuerpo("Lámpara favorita"));
        var c = await Api.RegistrarAsync(_fabrica, "guarda");

        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/v1/publicaciones/{pub}/favorito", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync($"/api/v1/publicaciones/{pub}/favorito", null)).StatusCode); // idempotente
        Assert.True((await c.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}")).GetProperty("esFavorita").GetBoolean());
        Assert.False((await duenio.GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones/{pub}")).GetProperty("esFavorita").GetBoolean());

        var favs = await c.GetFromJsonAsync<JsonElement>("/api/v1/favoritos");
        Assert.Equal(1, favs.GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"/api/v1/publicaciones/{pub}/favorito")).StatusCode);
        Assert.Equal(0, (await c.GetFromJsonAsync<JsonElement>("/api/v1/favoritos")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsync($"/api/v1/publicaciones/{Guid.NewGuid()}/favorito", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().PostAsync($"/api/v1/publicaciones/{pub}/favorito", null)).StatusCode);
    }

    [Fact]
    public async Task Orden_por_precio_y_filtro_de_rango()
    {
        var c = await Api.RegistrarAsync(_fabrica, "precios");
        var marca = Guid.NewGuid().ToString("N")[..8];
        await CrearAsync(c, Cuerpo($"Caro {marca}", "Compra", 300000));
        await CrearAsync(c, Cuerpo($"Barato {marca}", "Compra", 10000));
        await CrearAsync(c, Cuerpo($"Medio {marca}", "Compra", 50000));

        async Task<string[]> Titulos(string query)
            => (await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones?texto={marca}&{query}"))
                .GetProperty("items").EnumerateArray().Select(p => p.GetProperty("titulo").GetString()!.Split(' ')[0]).ToArray();

        Assert.Equal(new[] { "Barato", "Medio", "Caro" }, await Titulos("orden=PrecioAsc"));
        Assert.Equal(new[] { "Caro", "Medio", "Barato" }, await Titulos("orden=PrecioDesc"));
        Assert.Equal(new[] { "Medio" }, await Titulos("precioMin=20000&precioMax=100000"));
        Assert.Equal(HttpStatusCode.BadRequest, (await _fabrica.CreateClient().GetAsync("/api/v1/publicaciones?precioMin=10&precioMax=5")).StatusCode);
    }

    [Fact]
    public async Task La_busqueda_ignora_tildes_mayusculas_orden_y_plurales_y_prioriza_el_titulo()
    {
        var c = await Api.RegistrarAsync(_fabrica, "buscable");
        var marca = "zq" + Guid.NewGuid().ToString("N")[..6]; // palabra única para aislar esta prueba
        var enTitulo = await CrearAsync(c, new { titulo = $"Cámara fotográfica {marca}", descripcion = "Funciona bien", categoriaId = 5,
            modo = "Trueke", condicion = "Usado", localidad = "Suba" });
        var enDescripcion = await CrearAsync(c, new { titulo = $"Trípode {marca}", descripcion = "Ideal para una cámara", categoriaId = 5,
            modo = "Trueke", condicion = "Usado", localidad = "Suba" });
        await CrearAsync(c, new { titulo = $"Zapatos de cuero {marca}", descripcion = "Talla 40", categoriaId = 1,
            modo = "Trueke", condicion = "Usado", localidad = "Suba" });

        async Task<Guid[]> Buscar(string texto)
            => (await _fabrica.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/publicaciones?texto={Uri.EscapeDataString(texto)}"))
                .GetProperty("items").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToArray();

        // sin tildes ni mayúsculas; la que lo tiene en el título va primero aunque sea más antigua
        Assert.Equal(new[] { enTitulo, enDescripcion }, await Buscar($"CAMARA {marca}"));
        Assert.Equal(new[] { enTitulo }, await Buscar($"{marca} fotografica"));      // palabras en otro orden
        Assert.Single(await Buscar($"zapato {marca}"));                               // singular encuentra el plural
        Assert.Single(await Buscar($"los zapatos {marca}"));                          // y el plural, sin "los"
        Assert.Single(await Buscar($"ropa {marca}"));                                 // por el nombre de la categoría
        Assert.Empty(await Buscar($"mara {marca}"));                                  // solo al inicio de palabra
        Assert.Empty(await Buscar($"camara {marca} nevera"));                         // todas las palabras deben estar
    }

    [Fact]
    public async Task Sugerencias_publicas_sin_repetir_y_solo_de_publicaciones_visibles()
    {
        var c = await Api.RegistrarAsync(_fabrica, "sugiere");
        var marca = "zk" + Guid.NewGuid().ToString("N")[..6];
        await CrearAsync(c, Cuerpo($"Lámpara {marca}"));
        await CrearAsync(c, Cuerpo($"lampara {marca}"));
        await CrearAsync(c, Cuerpo($"Lámpara de pie {marca}"));
        var cancelada = await CrearAsync(c, Cuerpo($"Lámpara rota {marca}"));
        (await c.PostAsJsonAsync($"/api/v1/publicaciones/{cancelada}/cancelar", new { motivo = "Ya no" })).EnsureSuccessStatusCode();

        var anonimo = _fabrica.CreateClient();
        var r = await anonimo.GetFromJsonAsync<string[]>($"/api/v1/publicaciones/sugerencias?texto=lampa%20{marca}");
        Assert.Equal(2, r!.Length); // "Lámpara X" y "lampara X" son la misma sugerencia; la cancelada no aparece
        Assert.Contains($"Lámpara de pie {marca}", r);
        Assert.Empty((await anonimo.GetFromJsonAsync<string[]>("/api/v1/publicaciones/sugerencias?texto=l"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonimo.GetAsync("/api/v1/publicaciones/sugerencias?texto=x&max=50")).StatusCode);
    }
}
