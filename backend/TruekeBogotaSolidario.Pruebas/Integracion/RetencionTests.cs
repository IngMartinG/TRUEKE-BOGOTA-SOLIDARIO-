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
/// Retención: "¿Sigue disponible?" (pregunta a los 30 días sin actividad y pausa 7 días después), resumen semanal opcional,
/// metas comunitarias en /impacto (con el mínimo de 3 intercambios) y el logro de 100 kg.
/// </summary>
public class RetencionTests : IClassFixture<FabricaApi>
{
    private readonly RelojFalso _reloj = new();
    private readonly WebApplicationFactory<Program> _f;

    public RetencionTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_reloj))));

    private static string Marca() => "rt" + Guid.NewGuid().ToString("N")[..6];

    private async Task<HttpClient> SuperAsync() => Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private static async Task<Guid> PublicarAsync(HttpClient c, string titulo, string localidad = "Chapinero", decimal? kg = null)
    {
        var r = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo, descripcion = "Para la prueba", categoriaId = 1, modo = "Donacion", condicion = "Usado", localidad, pesoEstimadoKg = kg
        });
        Assert.True(r.StatusCode == HttpStatusCode.Created, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task CompletarAsync(HttpClient duenio, HttpClient otro, Guid pub)
    {
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Me interesa" });
        var id = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/aceptar", null)).EnsureSuccessStatusCode();
        (await duenio.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
        (await otro.PostAsync($"/api/v1/solicitudes/{id}/confirmar-entrega", null)).EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> MiaAsync(HttpClient c, Guid id)
        => (await c.GetFromJsonAsync<JsonElement[]>("/api/v1/publicaciones/mias"))!.Single(p => p.GetProperty("id").GetGuid() == id);

    private static async Task<string[]> AvisosAsync(HttpClient c, string tipo)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray()
            .Where(n => n.GetProperty("tipo").GetString() == tipo).Select(n => n.GetProperty("mensaje").GetString()!).ToArray();

    private async Task RetenerAsync(Func<IRetencionService, Task<int>> paso)
    {
        using var scope = _f.Services.CreateScope();
        await paso(scope.ServiceProvider.GetRequiredService<IRetencionService>());
    }

    [Fact]
    public async Task Pregunta_a_los_30_dias_pausa_a_los_7_mas_y_el_duenio_la_reactiva()
    {
        var sesion = await Api.RegistrarSesionAsync(_f, "duenioInactivo");
        var duenio = Api.ConToken(_f, sesion.Token);
        var otro = await Api.RegistrarAsync(_f, "vecinoCurioso");
        var titulo = "Lámpara " + Marca();
        var pub = await PublicarAsync(duenio, titulo);

        // Antes de los 30 días no pasa nada
        _reloj.Avanzar(TimeSpan.FromDays(29));
        await RetenerAsync(r => r.PreguntarDisponibilidadAsync(500));
        Assert.Equal(JsonValueKind.Null, (await MiaAsync(duenio, pub)).GetProperty("disponibilidadPedidaUtc").ValueKind);

        // A los 30: notificación y correo con el título, y nadie más puede responder por el dueño
        _reloj.Avanzar(TimeSpan.FromDays(2));
        await RetenerAsync(r => r.PreguntarDisponibilidadAsync(500));
        Assert.NotEqual(JsonValueKind.Null, (await MiaAsync(duenio, pub)).GetProperty("disponibilidadPedidaUtc").ValueKind);
        Assert.Contains(await AvisosAsync(duenio, "PublicacionSigueDisponible"), m => m.Contains(titulo));
        Assert.Contains(Api.Correos(_f).Para(sesion.Usuario.Correo), m => m.Asunto == "¿Tus publicaciones siguen disponibles?" && m.Texto.Contains(titulo));
        Assert.Equal(HttpStatusCode.NotFound, (await otro.PostAsync($"/api/v1/publicaciones/{pub}/sigue-disponible", null)).StatusCode);

        // Confirmar borra la pregunta y reinicia la cuenta de 30 días
        Assert.Equal(HttpStatusCode.OK, (await duenio.PostAsync($"/api/v1/publicaciones/{pub}/sigue-disponible", null)).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await MiaAsync(duenio, pub)).GetProperty("disponibilidadPedidaUtc").ValueKind);
        _reloj.Avanzar(TimeSpan.FromDays(8));
        await RetenerAsync(r => r.PausarSinRespuestaAsync(500));
        Assert.Equal("Disponible", (await MiaAsync(duenio, pub)).GetProperty("estado").GetString());

        // Sin respuesta: se pregunta de nuevo y a los 7 días se pausa (sale del catálogo)
        _reloj.Avanzar(TimeSpan.FromDays(23));
        await RetenerAsync(r => r.PreguntarDisponibilidadAsync(500));
        _reloj.Avanzar(TimeSpan.FromDays(6));
        await RetenerAsync(r => r.PausarSinRespuestaAsync(500));
        Assert.Equal("Disponible", (await MiaAsync(duenio, pub)).GetProperty("estado").GetString());
        _reloj.Avanzar(TimeSpan.FromDays(2));
        await RetenerAsync(r => r.PausarSinRespuestaAsync(500));
        Assert.Equal("Pausada", (await MiaAsync(duenio, pub)).GetProperty("estado").GetString());
        Assert.Contains(await AvisosAsync(duenio, "PublicacionPausada"), m => m.Contains(titulo));
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Aún la tienes?" });
        Assert.False(resp.IsSuccessStatusCode);

        // Reactivar la devuelve al catálogo
        Assert.Equal(HttpStatusCode.OK, (await duenio.PostAsync($"/api/v1/publicaciones/{pub}/sigue-disponible", null)).StatusCode);
        Assert.Equal("Disponible", (await MiaAsync(duenio, pub)).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.OK, (await otro.GetAsync($"/api/v1/publicaciones/{pub}")).StatusCode);
    }

    [Fact]
    public async Task Editarla_responde_la_pregunta()
    {
        var duenio = await Api.RegistrarAsync(_f, "duenioQueEdita");
        var titulo = "Silla " + Marca();
        var pub = await PublicarAsync(duenio, titulo);
        _reloj.Avanzar(TimeSpan.FromDays(31));
        await RetenerAsync(r => r.PreguntarDisponibilidadAsync(500));
        Assert.NotEqual(JsonValueKind.Null, (await MiaAsync(duenio, pub)).GetProperty("disponibilidadPedidaUtc").ValueKind);
        var r = await duenio.PutAsJsonAsync($"/api/v1/publicaciones/{pub}", new
        {
            titulo, descripcion = "Ahora con cojín", categoriaId = 1, modo = "Donacion", condicion = "Usado", localidad = "Chapinero"
        });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, (await MiaAsync(duenio, pub)).GetProperty("disponibilidadPedidaUtc").ValueKind);
        _reloj.Avanzar(TimeSpan.FromDays(8));
        await RetenerAsync(r => r.PausarSinRespuestaAsync(500));
        Assert.Equal("Disponible", (await MiaAsync(duenio, pub)).GetProperty("estado").GetString());
    }

    [Fact]
    public async Task El_resumen_semanal_esta_apagado_por_defecto_y_llega_una_vez_por_semana()
    {
        var sesion = await Api.RegistrarSesionAsync(_f, "lectoraResumen");
        var lectora = Api.ConToken(_f, sesion.Token);
        var sinResumen = await Api.RegistrarSesionAsync(_f, "sinResumen");
        var pref = await lectora.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo/preferencias-avisos");
        Assert.False(pref.GetProperty("resumenSemanal").GetBoolean());

        var r = await lectora.PutAsJsonAsync("/api/v1/usuarios/yo/preferencias-avisos",
            new { intercambios = true, mensajes = true, planes = true, novedades = false, resumenSemanal = true });
        Assert.True((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resumenSemanal").GetBoolean());
        var deseo = "Bicicleta " + Marca();
        (await lectora.PostAsJsonAsync("/api/v1/deseos", new { texto = deseo })).EnsureSuccessStatusCode();

        var vecino = await Api.RegistrarAsync(_f, "vecinoQuePublica");
        var titulo = "Mesa " + Marca();
        await PublicarAsync(vecino, titulo); // en Chapinero, como la lectora
        await PublicarAsync(vecino, deseo);
        _reloj.Avanzar(TimeSpan.FromMinutes(1)); // las publicaciones toman la hora real: el reloj falso debe ir después
        await RetenerAsync(s => s.EnviarResumenesSemanalesAsync(500));

        var resumen = Assert.Single(Api.Correos(_f).Para(sesion.Usuario.Correo), m => m.Asunto == "Tu semana en Trueke");
        Assert.Contains(titulo, resumen.Texto);
        Assert.Contains($"«{deseo}»: 1 nueva", resumen.Texto);
        Assert.DoesNotContain(Api.Correos(_f).Para(sinResumen.Usuario.Correo), m => m.Asunto == "Tu semana en Trueke");

        // Dentro de la misma semana no se repite; a la siguiente, sin novedades, no se escribe
        await RetenerAsync(s => s.EnviarResumenesSemanalesAsync(500));
        _reloj.Avanzar(TimeSpan.FromDays(8));
        await RetenerAsync(s => s.EnviarResumenesSemanalesAsync(500));
        Assert.Single(Api.Correos(_f).Para(sesion.Usuario.Correo), m => m.Asunto == "Tu semana en Trueke");
    }

    [Fact]
    public async Task La_meta_comunitaria_la_fija_el_SuperUsuario_y_muestra_el_avance_del_mes()
    {
        var super = await SuperAsync();
        var hugo = await Api.RegistrarAsync(_f, "hugoMeta");
        var otro = await Api.RegistrarAsync(_f, "otroMeta");
        const string localidad = "Usme";

        // Sin 3 intercambios en la localidad no se puede fijar
        var sinDatos = await super.PutAsJsonAsync("/api/v1/admin/impacto/metas", new { localidad, metaKg = 1000 });
        Assert.Equal(HttpStatusCode.BadRequest, sinDatos.StatusCode);

        for (var i = 0; i < 3; i++) await CompletarAsync(hugo, otro, await PublicarAsync(hugo, "Cajón " + Marca(), localidad, kg: 40));

        Assert.Equal(HttpStatusCode.Forbidden, (await hugo.PutAsJsonAsync("/api/v1/admin/impacto/metas", new { localidad, metaKg = 1000 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await super.PutAsJsonAsync("/api/v1/admin/impacto/metas", new { localidad, metaKg = 1 })).StatusCode);
        var r = await super.PutAsJsonAsync("/api/v1/admin/impacto/metas", new { localidad, metaKg = 1000 });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var meta = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(120m, meta.GetProperty("kgEsteMes").GetDecimal());
        Assert.Equal(12, meta.GetProperty("porcentaje").GetInt32());

        // Guardar otra vez la cambia (no la duplica) y /impacto la muestra al público
        (await super.PutAsJsonAsync("/api/v1/admin/impacto/metas", new { localidad, metaKg = 240 })).EnsureSuccessStatusCode();
        var impacto = await _f.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/impacto");
        var publica = Assert.Single(impacto.GetProperty("metas").EnumerateArray(), m => m.GetProperty("localidad").GetString() == localidad);
        Assert.Equal(240m, publica.GetProperty("metaKg").GetDecimal());
        Assert.Equal(50, publica.GetProperty("porcentaje").GetInt32());
        Assert.False(publica.GetProperty("reservado").GetBoolean());

        // 120 kg entregados: el logro de 100 kg
        var logros = await hugo.GetFromJsonAsync<JsonElement[]>("/api/v1/eco-puntos/logros");
        Assert.True(logros!.Single(l => l.GetProperty("clave").GetString() == "cien-kg").GetProperty("obtenido").GetBoolean());

        var id = publica.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await super.DeleteAsync($"/api/v1/admin/impacto/metas/{id}")).StatusCode);
        impacto = await _f.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/impacto");
        Assert.DoesNotContain(impacto.GetProperty("metas").EnumerateArray(), m => m.GetProperty("localidad").GetString() == localidad);
    }
}
