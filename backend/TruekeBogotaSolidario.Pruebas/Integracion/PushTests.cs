using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Push;
using TruekeBogotaSolidario.Pruebas.Infraestructura;
using TruekeBogotaSolidario.Pruebas.Unitarias;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Notificaciones push: dispositivos de cada persona y avisos sin datos privados. No sale nada a internet.</summary>
public class PushTests : IClassFixture<FabricaApi>
{
    private static readonly (string Publica, string Privada) Llaves = PushReglasTests.LlavesVapid();
    private readonly WebApplicationFactory<Program> _f;

    public PushTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b =>
        {
            b.UseSetting("Push:ClavePublica", Llaves.Publica);
            b.UseSetting("Push:ClavePrivada", Llaves.Privada);
            // Sin el despachador: los avisos quedan en la cola y la prueba los revisa (no se llama a Google/Apple).
            b.ConfigureTestServices(s =>
            {
                var envio = s.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(EnvioPushHostedService));
                s.Remove(envio);
            });
        });

    private static string Endpoint() => $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}";
    private static object Suscripcion(string endpoint) => new { endpoint, p256dh = new string('B', 87), auth = new string('a', 22) };

    private async Task<IReadOnlyList<Datos.Entidades.SuscripcionPush>> DeAsync(Guid usuarioId)
    {
        using var scope = _f.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISuscripcionPushRepository>().DeUsuarioAsync(usuarioId);
    }

    private List<AvisoPush> Encolados()
    {
        var lector = _f.Services.GetRequiredService<ColaPush>().Lector;
        var lista = new List<AvisoPush>();
        while (lector.TryRead(out var a)) lista.Add(a);
        return lista;
    }

    [Fact]
    public async Task La_configuracion_publica_trae_la_llave_publica_y_nunca_la_privada()
    {
        var cuerpo = await _f.CreateClient().GetStringAsync("/api/v1/configuracion");
        Assert.Equal(Llaves.Publica, JsonDocument.Parse(cuerpo).RootElement.GetProperty("pushClavePublica").GetString());
        Assert.DoesNotContain(Llaves.Privada, cuerpo);
    }

    [Fact]
    public async Task Registra_el_dispositivo_y_lo_pasa_a_otra_cuenta_si_cambia_quien_usa_el_navegador()
    {
        var ana = await Api.RegistrarSesionAsync(_f, "anaPush");
        var beto = await Api.RegistrarSesionAsync(_f, "betoPush");
        var endpoint = Endpoint();

        Assert.Equal(HttpStatusCode.NoContent, (await Api.ConToken(_f, ana.Token).PutAsJsonAsync("/api/v1/push/suscripciones", Suscripcion(endpoint))).StatusCode);
        Assert.Single(await DeAsync(ana.Usuario.Id));

        // Beto inicia sesión en el mismo navegador: a Ana ya no le llegan avisos ahí
        Assert.Equal(HttpStatusCode.NoContent, (await Api.ConToken(_f, beto.Token).PutAsJsonAsync("/api/v1/push/suscripciones", Suscripcion(endpoint))).StatusCode);
        Assert.Empty(await DeAsync(ana.Usuario.Id));
        Assert.Single(await DeAsync(beto.Usuario.Id));

        // Ana no puede dar de baja el dispositivo de Beto
        await Api.ConToken(_f, ana.Token).PostAsJsonAsync("/api/v1/push/suscripciones/baja", new { endpoint });
        Assert.Single(await DeAsync(beto.Usuario.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await Api.ConToken(_f, beto.Token).PostAsJsonAsync("/api/v1/push/suscripciones/baja", new { endpoint })).StatusCode);
        Assert.Empty(await DeAsync(beto.Usuario.Id));
    }

    [Fact]
    public async Task Rechaza_endpoints_que_no_son_servicios_de_push_y_exige_sesion()
    {
        var c = await Api.RegistrarAsync(_f, "ssrf");
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/v1/push/suscripciones", Suscripcion("https://169.254.169.254/latest/meta-data/x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync("/api/v1/push/suscripciones", new { endpoint = Endpoint() })).StatusCode); // sin llaves
        Assert.Equal(HttpStatusCode.Unauthorized, (await _f.CreateClient().PutAsJsonAsync("/api/v1/push/suscripciones", Suscripcion(Endpoint()))).StatusCode);
    }

    [Fact]
    public async Task Cerrar_todas_las_sesiones_quita_todos_los_dispositivos()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "perdioCelular");
        var c = Api.ConToken(_f, ses.Token);
        await c.PutAsJsonAsync("/api/v1/push/suscripciones", Suscripcion(Endpoint()));
        await c.PutAsJsonAsync("/api/v1/push/suscripciones", Suscripcion(Endpoint()));
        Assert.Equal(2, (await DeAsync(ses.Usuario.Id)).Count);

        (await c.PostAsync("/api/v1/auth/cerrar-sesiones", null)).EnsureSuccessStatusCode();
        Assert.Empty(await DeAsync(ses.Usuario.Id));
    }

    [Fact]
    public async Task Una_solicitud_nueva_avisa_al_duenio_que_no_esta_en_linea_sin_datos_privados()
    {
        var duenio = await Api.RegistrarSesionAsync(_f, "duenioPush");
        var pub = await Api.CrearPublicacionAsync(Api.ConToken(_f, duenio.Token));
        Encolados(); // descartar lo anterior

        var otro = await Api.RegistrarAsync(_f, "interesadoPush");
        (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Mi número es 3001234567" })).EnsureSuccessStatusCode();

        var aviso = Assert.Single(Encolados(), a => a.UsuarioId == duenio.Usuario.Id);
        Assert.Equal("Novedades en tu intercambio", aviso.Titulo);
        Assert.Equal("intercambios", aviso.Ruta);
        Assert.DoesNotContain("3001234567", aviso.Cuerpo); // el mensaje de la solicitud no viaja en el aviso
    }
}
