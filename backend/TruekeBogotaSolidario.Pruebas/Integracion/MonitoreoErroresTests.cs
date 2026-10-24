using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Presentacion.Seguridad;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Sentry recibe los 500 (no los 4xx) y nunca datos personales ni tokens; sin Sentry:Dsn queda apagado.</summary>
public class MonitoreoErroresTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public MonitoreoErroresTests(FabricaApi fabrica) => _fabrica = fabrica;

    private sealed class TransporteCapturado : ITransport
    {
        public ConcurrentQueue<string> Envios { get; } = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var flujo = new MemoryStream();
            await envelope.SerializeAsync(flujo, null, cancellationToken);
            Envios.Enqueue(Encoding.UTF8.GetString(flujo.ToArray()));
        }
    }

    private sealed class EcoPuntosQueFalla : IEcoPuntosService
    {
        public Task<EcoPuntosResumenDto> ResumenAsync(Guid actorId) => throw new NotImplementedException();
        public PoliticaEcoPuntosDto ObtenerPolitica() => throw new InvalidOperationException("Falló al avisar a persona.real@correo.co");
        public Task<PaginaDto<MovimientoEcoPuntosDto>> MovimientosAsync(Guid actorId, int pagina, int tamano) => throw new NotImplementedException();
        public Task<InvitacionDto> InvitacionAsync(Guid actorId) => throw new NotImplementedException();
        public Task<EcoPuntosResumenDto> ActivarPruebaPremiumAsync(Guid actorId) => throw new NotImplementedException();
    }

    private static async Task<string> EsperarEnvioAsync(TransporteCapturado t)
    {
        for (var i = 0; i < 50 && t.Envios.IsEmpty; i++) await Task.Delay(100);
        return Assert.Single(t.Envios);
    }

    [Fact]
    public async Task Un_500_llega_a_Sentry_sin_datos_personales_ni_tokens()
    {
        var transporte = new TransporteCapturado();
        await using var f = _fabrica.WithWebHostBuilder(b =>
        {
            b.UseSetting("Sentry:Dsn", "https://clave@o1.ingest.sentry.io/1");
            b.ConfigureTestServices(s =>
            {
                s.Replace(ServiceDescriptor.Scoped<IEcoPuntosService, EcoPuntosQueFalla>());
                s.Configure<SentryAspNetCoreOptions>(o => o.Transport = transporte);
            });
        });
        var cliente = f.CreateClient();
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/eco-puntos/politica?access_token=TOKEN_SECRETO");
        peticion.Headers.Add("Cookie", "refresco=COOKIE_SECRETA");
        peticion.Headers.Add("Authorization", "Bearer JWT_SECRETO");

        Assert.Equal(HttpStatusCode.InternalServerError, (await cliente.SendAsync(peticion)).StatusCode);

        var envio = await EsperarEnvioAsync(transporte);
        Assert.Contains("InvalidOperationException", envio);
        Assert.Contains("/api/v1/eco-puntos/politica", envio);
        Assert.Contains("[correo]", envio);
        foreach (var secreto in new[] { "persona.real@correo.co", "TOKEN_SECRETO", "COOKIE_SECRETA", "JWT_SECRETO", "access_token" })
            Assert.DoesNotContain(secreto, envio);
    }

    [Fact]
    public async Task Un_404_de_dominio_no_se_reporta()
    {
        var transporte = new TransporteCapturado();
        await using var f = _fabrica.WithWebHostBuilder(b =>
        {
            b.UseSetting("Sentry:Dsn", "https://clave@o1.ingest.sentry.io/1");
            b.ConfigureTestServices(s => s.Configure<SentryAspNetCoreOptions>(o => o.Transport = transporte));
        });
        var r = await f.CreateClient().GetAsync($"/api/v1/publicaciones/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        await Task.Delay(500);
        Assert.Empty(transporte.Envios);
    }

    [Fact]
    public void Sin_Dsn_no_se_activa()
    {
        Assert.Null(_fabrica.Services.GetService<IHub>());
    }

    [Fact]
    public void Las_migas_pierden_la_query_y_los_correos()
    {
        var miga = new Breadcrumb("Aviso a ana@correo.co tras https://api.groq.com/v1?clave=SECRETO falló", "http",
            new Dictionary<string, string> { ["url"] = "https://x.supabase.co/storage/v1/object/upload/sign/a.jpg?token=SECRETO", ["method"] = "PUT" },
            "http", BreadcrumbLevel.Warning);

        var limpia = MonitoreoErrores.Limpiar(miga);

        Assert.Equal("Aviso a [correo] tras https://api.groq.com/v1 falló", limpia.Message);
        Assert.Equal("https://x.supabase.co/storage/v1/object/upload/sign/a.jpg", limpia.Data!["url"]);
        Assert.Equal("PUT", limpia.Data["method"]);
    }

    [Fact]
    public void Una_miga_sin_nada_que_quitar_queda_igual()
    {
        var miga = new Breadcrumb("Reintento", "default", null, "Trueke", BreadcrumbLevel.Warning);
        Assert.Same(miga, MonitoreoErrores.Limpiar(miga));
    }
}
