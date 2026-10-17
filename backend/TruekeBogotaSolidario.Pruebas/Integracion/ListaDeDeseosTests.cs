using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// "Lo que busco": máximo 10 deseos sin repetir, públicos en el perfil, coincidencias en los dos sentidos (truekes mutuos primero),
/// avisos de publicaciones nuevas y moderación por denuncia.
/// </summary>
public class ListaDeDeseosTests : IClassFixture<FabricaApi>
{
    private readonly RelojFalso _reloj = new();
    private readonly FabricaApi _fabrica;
    private readonly WebApplicationFactory<Program> _f;

    public ListaDeDeseosTests(FabricaApi fabrica)
    {
        _fabrica = fabrica;
        _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_reloj))));
    }

    /// <summary>Palabra única por prueba: la base de datos en memoria se comparte entre pruebas.</summary>
    private static string Marca() => "zd" + Guid.NewGuid().ToString("N")[..6];

    private static async Task<JsonElement> DesearAsync(HttpClient c, object cuerpo, HttpStatusCode esperado = HttpStatusCode.Created)
    {
        var r = await c.PostAsJsonAsync("/api/v1/deseos", cuerpo);
        Assert.True(r.StatusCode == esperado, await r.Content.ReadAsStringAsync());
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<JsonElement>() : default;
    }

    private static async Task<Guid> PublicarAsync(HttpClient c, string titulo, string modo = "Trueke")
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo, descripcion = "Para la prueba", categoriaId = 1, modo, condicion = "Usado", localidad = "Chapinero",
            precioReferenciaCop = modo == "Compra" ? 50000 : (decimal?)null
        });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement[]> CoincidenciasAsync(HttpClient c)
        => await c.GetFromJsonAsync<JsonElement[]>("/api/v1/deseos/coincidencias") ?? [];

    private async Task<int> RevisarAsync()
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDeseoService>().RevisarAvisosAsync(500);
    }

    [Fact]
    public async Task Crea_edita_y_borra_sin_repetir_y_solo_su_duenio_lo_toca()
    {
        var ana = await Api.RegistrarAsync(_f, "anaDeseo");
        var d = await DesearAsync(ana, new { texto = "Bicicleta de ruta", categoriaId = 1 });
        Assert.True(d.GetProperty("publico").GetBoolean());
        Assert.True(d.GetProperty("avisosActivos").GetBoolean());
        Assert.False(string.IsNullOrEmpty(d.GetProperty("categoria").GetString()));

        await DesearAsync(ana, new { texto = "bicicletas ruta", categoriaId = 1 }, HttpStatusCode.BadRequest); // mismas palabras
        await DesearAsync(ana, new { texto = "de la" }, HttpStatusCode.BadRequest);                            // sin palabras útiles
        await DesearAsync(ana, new { texto = "ab" }, HttpStatusCode.BadRequest);                               // muy corto
        await DesearAsync(ana, new { texto = "Algo", categoriaId = 99999 }, HttpStatusCode.BadRequest);

        var id = d.GetProperty("id").GetGuid();
        var editado = await ana.PutAsJsonAsync($"/api/v1/deseos/{id}", new { texto = "Bicicleta todoterreno", publico = false, avisos = false });
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        var e = await editado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bicicleta todoterreno", e.GetProperty("texto").GetString());
        Assert.False(e.GetProperty("publico").GetBoolean());
        Assert.Equal(JsonValueKind.Null, e.GetProperty("categoriaId").ValueKind);

        var beto = await Api.RegistrarAsync(_f, "betoDeseo");
        Assert.Equal(HttpStatusCode.NotFound, (await beto.DeleteAsync($"/api/v1/deseos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await beto.PutAsJsonAsync($"/api/v1/deseos/{id}", new { texto = "Robado" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/v1/deseos")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/v1/deseos/coincidencias")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/v1/deseos/{id}")).StatusCode);
        Assert.Empty(await ana.GetFromJsonAsync<JsonElement[]>("/api/v1/deseos") ?? []);
    }

    [Fact]
    public async Task Maximo_diez_deseos()
    {
        var c = await Api.RegistrarAsync(_f, "muchosDeseos");
        for (var i = 0; i < 10; i++) await DesearAsync(c, new { texto = $"objeto modelo{i}" });
        await DesearAsync(c, new { texto = "uno más" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sin_correo_verificado_no_se_puede_agregar()
    {
        var r = await _f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar", new
        {
            nombreCompleto = "Sin Verificar", localidad = "Chapinero", correo = $"sinverif-{Guid.NewGuid():N}@trueke.test",
            clave = Api.ClaveValida, aceptoPoliticaDatos = true
        });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var token = (await r.Content.ReadFromJsonAsync<SesionMinDto>())!.Token;
        Assert.Equal(HttpStatusCode.Forbidden, (await Api.ConToken(_f, token).PostAsJsonAsync("/api/v1/deseos", new { texto = "Una lámpara" })).StatusCode);
    }

    [Fact]
    public async Task El_perfil_muestra_solo_los_publicos_y_avisa_si_quien_mira_lo_tiene()
    {
        var marca = Marca();
        var ses = await Api.RegistrarSesionAsync(_f, "perfilDeseo");
        var duena = Api.ConToken(_f, ses.Token);
        await DesearAsync(duena, new { texto = $"Lámpara {marca}" });
        await DesearAsync(duena, new { texto = $"Secreto {marca}", publico = false });

        var visitante = await Api.RegistrarAsync(_f, "visitaDeseo");
        await PublicarAsync(visitante, $"Lampara de mesa {marca}");

        var perfil = await visitante.GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        var deseo = Assert.Single(perfil.GetProperty("deseos").EnumerateArray());
        Assert.Equal($"Lámpara {marca}", deseo.GetProperty("texto").GetString());
        Assert.True(deseo.GetProperty("loTienes").GetBoolean()); // sin tilde y con más palabras también coincide

        var anonimo = await _f.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.False(Assert.Single(anonimo.GetProperty("deseos").EnumerateArray()).GetProperty("loTienes").GetBoolean());
        Assert.DoesNotContain("Secreto", anonimo.GetRawText());
    }

    [Fact]
    public async Task Las_coincidencias_ponen_primero_los_truekes_mutuos_y_omiten_lo_propio_y_lo_bloqueado()
    {
        var marca = Marca();
        var ana = await Api.RegistrarAsync(_f, "anaCoincide");
        await DesearAsync(ana, new { texto = $"Bicicleta {marca}" });
        var casco = await PublicarAsync(ana, $"Casco {marca}");
        await PublicarAsync(ana, $"Bicicleta propia {marca}"); // lo propio nunca aparece

        // Carla vende una bicicleta (sin deseos): coincide, pero no es mutua
        var carla = await Api.RegistrarAsync(_f, "carlaCoincide");
        var deCarla = await PublicarAsync(carla, $"Bicicleta de Carla {marca}", "Compra");
        // Beto ofrece una bicicleta en trueke y busca un casco (público): trueke mutuo
        var beto = await Api.RegistrarAsync(_f, "betoCoincide");
        await DesearAsync(beto, new { texto = $"Casco {marca}" });
        var deBeto = await PublicarAsync(beto, $"Bicicleta de Beto {marca}");
        // Dora también busca un casco, pero en privado: no se revela, no es mutua
        var dora = await Api.RegistrarAsync(_f, "doraCoincide");
        await DesearAsync(dora, new { texto = $"Casco {marca}", publico = false });
        var deDora = await PublicarAsync(dora, $"Bicicleta de Dora {marca}");

        var lista = await CoincidenciasAsync(ana);
        Assert.Equal([deBeto, deDora, deCarla], lista.Select(x => x.GetProperty("publicacion").GetProperty("id").GetGuid()));
        var mutua = lista[0];
        Assert.True(mutua.GetProperty("mutua").GetBoolean());
        Assert.Equal($"Casco {marca}", mutua.GetProperty("suDeseo").GetString());
        Assert.Equal(casco, mutua.GetProperty("tuPublicacionId").GetGuid());
        Assert.Equal($"Bicicleta {marca}", mutua.GetProperty("tuDeseo").GetString());
        Assert.All(lista.Skip(1), x =>
        {
            Assert.False(x.GetProperty("mutua").GetBoolean());
            Assert.Equal(JsonValueKind.Null, x.GetProperty("suDeseo").ValueKind);
        });

        // Si Ana bloquea a Beto, su publicación deja de aparecer
        var betoId = (await beto.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PostAsync($"/api/v1/usuarios/{betoId}/bloqueo", null)).StatusCode);
        Assert.DoesNotContain(await CoincidenciasAsync(ana), x => x.GetProperty("publicacion").GetProperty("id").GetGuid() == deBeto);
    }

    [Fact]
    public async Task Avisa_una_vez_de_lo_nuevo_que_coincide_y_no_con_avisos_apagados()
    {
        var marca = Marca();
        var ana = await Api.RegistrarAsync(_f, "anaAvisoDeseo");
        var carla = await Api.RegistrarAsync(_f, "carlaAvisoDeseo");
        await DesearAsync(ana, new { texto = $"Guitarra {marca}" });
        await DesearAsync(carla, new { texto = $"Guitarra {marca}", avisos = false });

        var beto = await Api.RegistrarAsync(_f, "betoAvisoDeseo");
        var pub = await PublicarAsync(beto, $"Guitarra acústica {marca}");
        await PublicarAsync(ana, $"Guitarra eléctrica {marca}"); // la propia no cuenta

        Assert.Equal(0, await RevisarAsync()); // aún no pasa el intervalo
        _reloj.Avanzar(TimeSpan.FromHours(1));
        await RevisarAsync();

        static async Task<JsonElement[]> AvisosAsync(HttpClient c)
            => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray()
                .Where(n => n.GetProperty("tipo").GetString() == "DeseoCoincidencia").ToArray();
        var aviso = Assert.Single(await AvisosAsync(ana));
        Assert.Equal(pub, aviso.GetProperty("recursoId").GetGuid()); // una sola: lleva a la publicación
        Assert.Contains($"Guitarra acústica {marca}", aviso.GetProperty("mensaje").GetString());
        Assert.Empty(await AvisosAsync(carla));

        _reloj.Avanzar(TimeSpan.FromHours(1));
        await RevisarAsync();
        Assert.Single(await AvisosAsync(ana)); // nada nuevo: no se repite
    }

    [Fact]
    public async Task Un_deseo_denunciado_y_ocultado_deja_de_verse_y_su_duenio_ve_el_motivo()
    {
        var marca = Marca();
        var ses = await Api.RegistrarSesionAsync(_fabrica, "deseoDenunciado");
        var autor = Api.ConToken(_fabrica, ses.Token);
        var d = await DesearAsync(autor, new { texto = $"Algo ofensivo {marca}" });
        var id = d.GetProperty("id").GetGuid();

        var vecina = await Api.RegistrarAsync(_fabrica, "vecinaDeseo");
        Assert.Equal(HttpStatusCode.Created, (await vecina.PostAsJsonAsync("/api/v1/denuncias",
            new { tipo = "Deseo", objetivoId = id, motivo = "ContenidoInapropiado" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await autor.PostAsJsonAsync("/api/v1/denuncias",
            new { tipo = "Deseo", objetivoId = id, motivo = "Spam" })).StatusCode); // lo propio no se denuncia

        var super = Api.ConToken(_fabrica, await Api.LoginAsync(_fabrica, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));
        var grupo = (await super.GetFromJsonAsync<JsonElement>("/api/v1/admin/denuncias?estado=Pendiente"))
            .EnumerateArray().Single(g => g.GetProperty("objetivoId").GetGuid() == id);
        Assert.Contains(marca, grupo.GetProperty("vistaPrevia").GetString());
        (await super.PostAsJsonAsync($"/api/v1/admin/denuncias/{grupo.GetProperty("denunciaId").GetGuid()}/resolver",
            new { accion = "OcultarContenido", nota = "Lenguaje ofensivo" })).EnsureSuccessStatusCode();

        var perfil = await vecina.GetFromJsonAsync<JsonElement>($"/api/v1/usuarios/{ses.Usuario.Id}/perfil");
        Assert.Empty(perfil.GetProperty("deseos").EnumerateArray());
        var propio = Assert.Single(await autor.GetFromJsonAsync<JsonElement[]>("/api/v1/deseos") ?? []);
        Assert.True(propio.GetProperty("oculto").GetBoolean());
        Assert.Equal("Lenguaje ofensivo", propio.GetProperty("motivoOcultamiento").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await autor.PutAsJsonAsync($"/api/v1/deseos/{id}", new { texto = "Otra cosa" })).StatusCode);
    }

    [Fact]
    public async Task Exportar_mis_datos_incluye_la_lista_de_deseos()
    {
        var c = await Api.RegistrarAsync(_f, "exportaDeseo");
        await DesearAsync(c, new { texto = "Una hamaca" });
        var datos = await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/datos");
        Assert.Equal("Una hamaca", datos.GetProperty("deseos")[0].GetProperty("texto").GetString());
    }
}
