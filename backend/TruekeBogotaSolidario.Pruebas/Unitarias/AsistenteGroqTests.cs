using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Asistente;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Groq: la llave solo va en la cabecera, se envían las herramientas, se leen sus llamadas y cualquier falla es "no disponible".</summary>
public class AsistenteGroqTests
{
    private sealed class GroqSimulado : HttpMessageHandler
    {
        private readonly HttpStatusCode _estado;
        private readonly string _respuesta;
        private readonly bool _demora;
        public GroqSimulado(HttpStatusCode estado = HttpStatusCode.OK, string respuesta = "", bool demora = false)
        {
            _estado = estado; _respuesta = respuesta; _demora = demora;
        }
        public HttpRequestMessage? Peticion { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Peticion = request;
            Cuerpo = await request.Content!.ReadAsStringAsync(ct);
            if (_demora) throw new TaskCanceledException("Tiempo agotado");
            return new HttpResponseMessage(_estado) { Content = new StringContent(_respuesta) };
        }
    }

    private sealed class FabricaHttp : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public FabricaHttp(HttpMessageHandler handler) => _handler = handler;
        public string? Pedido { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Pedido = name;
            return new HttpClient(_handler, disposeHandler: false);
        }
    }

    private static ModeloGroq Crear(HttpMessageHandler groq, FabricaHttp? fabrica = null)
        => new(fabrica ?? new FabricaHttp(groq), Options.Create(new AsistenteOpciones
        {
            Proveedor = AsistenteOpciones.ProveedorGroq,
            Groq = { ClaveApi = "gsk_clave_de_prueba", Modelo = "modelo-prueba" }
        }));

    private static readonly MensajeModelo[] Conversacion =
    [
        new(RolMensaje.Sistema, "reglas"),
        new(RolMensaje.Usuario, "¿cuántos puntos tengo?"),
        new(RolMensaje.Asistente, "", [new LlamadaHerramienta("l1", "mi_resumen_eco_puntos", "{}")]),
        new(RolMensaje.Herramienta, """{"saldo":10}""", IdLlamada: "l1"),
    ];

    [Fact]
    public async Task Envia_modelo_mensajes_y_herramientas_con_la_llave_solo_en_la_cabecera()
    {
        var groq = new GroqSimulado(respuesta: """{"choices":[{"message":{"role":"assistant","content":"Tienes 10."}}]}""");
        var fabrica = new FabricaHttp(groq);

        var r = await Crear(groq, fabrica).CompletarAsync(Conversacion, AsistenteService.Herramientas, CancellationToken.None);

        Assert.Equal("Tienes 10.", r.Contenido);
        Assert.Null(r.Llamadas);
        Assert.Equal(ModeloGroq.ClienteHttp, fabrica.Pedido); // el cliente con timeout y sin redirecciones
        Assert.Equal("Bearer", groq.Peticion!.Headers.Authorization!.Scheme);
        Assert.Equal("gsk_clave_de_prueba", groq.Peticion.Headers.Authorization.Parameter);
        Assert.DoesNotContain("gsk_clave_de_prueba", groq.Cuerpo);

        var cuerpo = JsonDocument.Parse(groq.Cuerpo!).RootElement;
        Assert.Equal("modelo-prueba", cuerpo.GetProperty("model").GetString());
        Assert.Equal("low", cuerpo.GetProperty("reasoning_effort").GetString());
        var roles = cuerpo.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("role").GetString()!).ToArray();
        Assert.Equal(["system", "user", "assistant", "tool"], roles);
        Assert.Equal("l1", cuerpo.GetProperty("messages")[3].GetProperty("tool_call_id").GetString());
        Assert.Equal("mi_resumen_eco_puntos", cuerpo.GetProperty("messages")[2].GetProperty("tool_calls")[0].GetProperty("function").GetProperty("name").GetString());
        Assert.Equal(AsistenteService.Herramientas.Count, cuerpo.GetProperty("tools").GetArrayLength());
    }

    [Fact]
    public async Task Lee_las_llamadas_a_herramientas()
    {
        var groq = new GroqSimulado(respuesta: """
            {"choices":[{"message":{"role":"assistant","content":null,
              "tool_calls":[{"id":"c1","type":"function","function":{"name":"buscar_publicaciones","arguments":"{\"texto\":\"bici\"}"}}]}}]}
            """);

        var r = await Crear(groq).CompletarAsync(Conversacion, AsistenteService.Herramientas, CancellationToken.None);

        var llamada = Assert.Single(r.Llamadas!);
        Assert.Equal(("c1", "buscar_publicaciones", """{"texto":"bici"}"""), (llamada.Id, llamada.Nombre, llamada.ArgumentosJson));
        Assert.Equal("", r.Contenido);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Un_rechazo_de_Groq_es_no_disponible_sin_repetir_el_cuerpo(HttpStatusCode estado)
    {
        var groq = new GroqSimulado(estado, """{"error":{"message":"¿cuántos puntos tengo?"}}""");
        var ex = await Assert.ThrowsAsync<ModeloNoDisponibleException>(
            () => Crear(groq).CompletarAsync(Conversacion, [], CancellationToken.None));
        Assert.Contains(((int)estado).ToString(), ex.Message);
        Assert.DoesNotContain("puntos", ex.Message);
    }

    [Fact]
    public async Task La_demora_o_una_respuesta_rara_es_no_disponible()
    {
        await Assert.ThrowsAsync<ModeloNoDisponibleException>(
            () => Crear(new GroqSimulado(demora: true)).CompletarAsync(Conversacion, [], CancellationToken.None));
        await Assert.ThrowsAsync<ModeloNoDisponibleException>(
            () => Crear(new GroqSimulado(respuesta: """{"choices":[]}""")).CompletarAsync(Conversacion, [], CancellationToken.None));
        await Assert.ThrowsAsync<ModeloNoDisponibleException>(
            () => Crear(new GroqSimulado(respuesta: "no es json")).CompletarAsync(Conversacion, [], CancellationToken.None));
    }

    [Fact]
    public void Solo_quedan_hasta_tres_enlaces_a_rutas_de_la_app()
    {
        var id = Guid.NewGuid();
        var r = AsistenteService.Armar($"""
            Mira esto.
            [[/explorar|Explorar]]
            [[/publicacion/{id}|Ver la bicicleta]]
            [[/publicacion/no-es-un-id|Raro]]
            [[javascript:alert(1)|Clic]]
            [[//sitio-malo.example|Externo]]
            [[/explorar|Repetido]]
            [[/planes|Planes]]
            [[/eco-puntos|De más]]
            """);

        Assert.Equal("Mira esto.", r.Texto);
        Assert.Equal(["/explorar", $"/publicacion/{id}", "/planes"], r.Enlaces.Select(e => e.Ruta));
    }

    [Fact]
    public void Sin_texto_ofrece_la_PQRS()
        => Assert.Contains("PQRS", AsistenteService.Armar("[[/ayuda|Ayuda]]").Texto);
}
