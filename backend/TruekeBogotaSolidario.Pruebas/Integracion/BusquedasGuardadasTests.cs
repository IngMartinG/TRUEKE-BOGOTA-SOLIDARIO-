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

/// <summary>Búsquedas guardadas: máximo 10, sin duplicados, solo de su dueño y con alertas de publicaciones NUEVAS.</summary>
public class BusquedasGuardadasTests : IClassFixture<FabricaApi>
{
    private readonly RelojFalso _reloj = new();
    private readonly WebApplicationFactory<Program> _f;

    public BusquedasGuardadasTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_reloj))));

    private static async Task<JsonElement> GuardarAsync(HttpClient c, object cuerpo, HttpStatusCode esperado = HttpStatusCode.Created)
    {
        var r = await c.PostAsJsonAsync("/api/v1/busquedas-guardadas", cuerpo);
        Assert.True(r.StatusCode == esperado, await r.Content.ReadAsStringAsync());
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<JsonElement>() : default;
    }

    private async Task<int> RevisarAsync()
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBusquedaGuardadaService>().RevisarAlertasAsync(500);
    }

    private static async Task<string[]> AvisosAsync(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("tipo").GetString() == "BusquedaGuardada").Select(n => n.GetProperty("mensaje").GetString()!).ToArray();

    [Fact]
    public async Task Guarda_sin_duplicar_con_nombre_legible_y_solo_su_duenio_la_ve_o_la_borra()
    {
        var ana = await Api.RegistrarAsync(_f, "anaBg");
        var b = await GuardarAsync(ana, new { texto = "Bicicleta", modo = "Trueke", municipioCodigo = "11001", precioMax = 200000 });
        Assert.Equal("Bicicleta · Trueke · Bogotá, D.C. · hasta $200.000", b.GetProperty("nombre").GetString());
        Assert.True(b.GetProperty("alertasActivas").GetBoolean());

        var otraVez = await GuardarAsync(ana, new { texto = "bicicleta", modo = "Trueke", municipioCodigo = "11001", precioMax = 200000 }, HttpStatusCode.Created);
        Assert.Equal(b.GetProperty("id").GetGuid(), otraVez.GetProperty("id").GetGuid()); // misma búsqueda: no se duplica
        Assert.Single(await ana.GetFromJsonAsync<JsonElement[]>("/api/v1/busquedas-guardadas") ?? []);

        await GuardarAsync(ana, new { }, HttpStatusCode.BadRequest); // sin texto ni filtros
        var beto = await Api.RegistrarAsync(_f, "betoBg");
        var id = b.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await beto.DeleteAsync($"/api/v1/busquedas-guardadas/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await beto.PutAsJsonAsync($"/api/v1/busquedas-guardadas/{id}/alertas", new { activas = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().GetAsync("/api/v1/busquedas-guardadas")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/v1/busquedas-guardadas/{id}")).StatusCode);
    }

    [Fact]
    public async Task Maximo_diez_busquedas()
    {
        var c = await Api.RegistrarAsync(_f, "muchasBg");
        for (var i = 0; i < 10; i++) await GuardarAsync(c, new { texto = $"objeto {i}" });
        await GuardarAsync(c, new { texto = "una más" }, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Avisa_una_vez_de_lo_nuevo_que_coincide_sin_contar_lo_propio_ni_con_alertas_apagadas()
    {
        var ana = await Api.RegistrarAsync(_f, "anaAlerta");
        var carla = await Api.RegistrarAsync(_f, "carlaAlerta");
        var marca = "zb" + Guid.NewGuid().ToString("N")[..6];
        await GuardarAsync(ana, new { texto = marca });
        var apagada = await GuardarAsync(carla, new { texto = marca, alertas = false });

        // Después de guardar: dos publicaciones que coinciden (una es de Ana) y una que no
        var beto = await Api.RegistrarAsync(_f, "betoAlerta");
        foreach (var (c, titulo) in new[] { (beto, $"Bicicleta {marca}"), (ana, $"Casco {marca}"), (beto, "Otra cosa distinta") })
            (await c.PostAsJsonAsync("/api/v1/publicaciones", new
            {
                titulo, descripcion = "Para la prueba", categoriaId = 1, modo = "Trueke", condicion = "Usado", localidad = "Chapinero"
            })).EnsureSuccessStatusCode();

        Assert.Equal(0, await RevisarAsync()); // aún no pasa el intervalo: no se revisa
        _reloj.Avanzar(TimeSpan.FromHours(1));
        await RevisarAsync();

        Assert.Equal([$"Hay 1 publicación nueva para tu búsqueda «{marca}»."], await AvisosAsync(ana)); // la propia no cuenta
        Assert.Empty(await AvisosAsync(carla));                                                     // alertas apagadas

        // Nada nuevo: no se repite el aviso
        _reloj.Avanzar(TimeSpan.FromHours(1));
        await RevisarAsync();
        Assert.Single(await AvisosAsync(ana));

        // Al encenderlas, no avisa de lo publicado mientras estuvieron apagadas
        (await carla.PutAsJsonAsync($"/api/v1/busquedas-guardadas/{apagada.GetProperty("id").GetGuid()}/alertas", new { activas = true })).EnsureSuccessStatusCode();
        _reloj.Avanzar(TimeSpan.FromHours(1));
        await RevisarAsync();
        Assert.Empty(await AvisosAsync(carla));
    }

    [Fact]
    public async Task Exportar_mis_datos_incluye_las_busquedas_guardadas()
    {
        var c = await Api.RegistrarAsync(_f, "exportaBg");
        await GuardarAsync(c, new { texto = "lámpara" });
        var datos = await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/datos");
        Assert.Equal("lámpara", datos.GetProperty("busquedasGuardadas")[0].GetProperty("texto").GetString());
    }
}
