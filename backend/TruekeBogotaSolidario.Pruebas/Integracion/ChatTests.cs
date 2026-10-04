using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class ChatTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public ChatTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<(HttpClient Duenio, HttpClient Otro, SesionMinDto SesOtro, Guid SolicitudId, Guid ConversacionId)> PrepararAsync()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "duenia");
        var sesOtro = await Api.RegistrarSesionAsync(_fabrica, "interesado");
        var otro = Api.ConToken(_fabrica, sesOtro.Token);
        var pub = await Api.CrearPublicacionAsync(duenio);
        var r = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Me la cambias por un libro?" });
        var s = await r.Content.ReadFromJsonAsync<JsonElement>();
        return (duenio, otro, sesOtro, s.GetProperty("id").GetGuid(), s.GetProperty("conversacionId").GetGuid());
    }

    [Fact]
    public async Task La_solicitud_crea_el_chat_con_su_mensaje_y_ambos_pueden_conversar()
    {
        var (duenio, otro, _, _, conv) = await PrepararAsync();

        var bandeja = await duenio.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones");
        var c = bandeja.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == conv);
        Assert.Equal(1, c.GetProperty("noLeidos").GetInt32());
        Assert.True(c.GetProperty("soyDuenio").GetBoolean());
        Assert.True(c.GetProperty("escribible").GetBoolean());
        Assert.Equal("interesado P.", c.GetProperty("contraparte").GetProperty("nombre").GetString());

        Assert.Equal(HttpStatusCode.Created, (await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "¡Claro! ¿Cuándo nos vemos?" })).StatusCode);
        var mensajes = await otro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes");
        Assert.Equal(2, mensajes!.Count);
        Assert.True(mensajes[0].EsMio);                           // el de la solicitud lo escribió "otro"
        Assert.False(mensajes[1].EsMio);
        Assert.Equal("¡Claro! ¿Cuándo nos vemos?", mensajes[1].Texto);

        (await duenio.PostAsync($"/api/v1/conversaciones/{conv}/leer", null)).EnsureSuccessStatusCode();
        var tras = await duenio.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones");
        Assert.Equal(0, tras.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == conv).GetProperty("noLeidos").GetInt32());
    }

    [Fact]
    public async Task Un_tercero_no_puede_ver_ni_escribir_en_el_chat()
    {
        var (_, _, _, _, conv) = await PrepararAsync();
        var intruso = await Api.RegistrarAsync(_fabrica, "intruso");
        Assert.Equal(HttpStatusCode.NotFound, (await intruso.GetAsync($"/api/v1/conversaciones/{conv}/mensajes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruso.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruso.PostAsync($"/api/v1/conversaciones/{conv}/leer", null)).StatusCode);
        Assert.DoesNotContain(conv.ToString(), await intruso.GetStringAsync("/api/v1/conversaciones"));
    }

    [Fact]
    public async Task Tras_rechazar_la_solicitud_el_chat_queda_de_solo_lectura()
    {
        var (duenio, otro, _, solicitud, conv) = await PrepararAsync();
        (await duenio.PostAsJsonAsync($"/api/v1/solicitudes/{solicitud}/rechazar", new { motivo = "Ya no" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await otro.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "¿Por qué?" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otro.GetAsync($"/api/v1/conversaciones/{conv}/mensajes")).StatusCode);
    }

    [Fact]
    public async Task Tras_aceptar_se_puede_seguir_conversando_y_no_se_exponen_correos()
    {
        var (duenio, otro, sesOtro, solicitud, conv) = await PrepararAsync();
        var aceptada = await duenio.PostAsync($"/api/v1/solicitudes/{solicitud}/aceptar", null);
        var cuerpo = await aceptada.Content.ReadAsStringAsync();
        Assert.DoesNotContain("@", cuerpo);
        Assert.DoesNotContain("correoContacto", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", await otro.GetStringAsync("/api/v1/solicitudes/enviadas"));
        Assert.DoesNotContain(sesOtro.Usuario.Correo, await duenio.GetStringAsync("/api/v1/solicitudes/recibidas"));
        Assert.Equal(HttpStatusCode.Created, (await otro.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "Nos vemos el sábado" })).StatusCode);
    }

    [Fact]
    public async Task El_mensaje_llega_en_tiempo_real_solo_a_la_contraparte()
    {
        var (duenio, _, sesOtro, _, conv) = await PrepararAsync();
        var recibidos = new ConcurrentQueue<MensajeChatDto>();
        await using var conexion = new HubConnectionBuilder()
            .WithUrl(new Uri(_fabrica.Server.BaseAddress, "hubs/notificaciones"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _fabrica.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(sesOtro.Token);
            }).Build();
        conexion.On<MensajeChatDto>("mensaje", m => recibidos.Enqueue(m));
        await conexion.StartAsync();

        await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "Hola desde el chat" });
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (recibidos.IsEmpty && DateTime.UtcNow < limite) await Task.Delay(50);
        var m = Assert.Single(recibidos);
        Assert.Equal("Hola desde el chat", m.Texto);
        Assert.False(m.EsMio);
        Assert.Equal(conv, m.ConversacionId);
    }

    [Fact]
    public async Task Se_puede_responder_un_mensaje_en_particular_de_la_misma_conversacion()
    {
        var (duenio, otro, _, _, conv) = await PrepararAsync();
        var original = (await otro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Single();

        var r = await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "¡Sí, me interesa el libro!", respuestaAId = original.Id });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var enviada = (await r.Content.ReadFromJsonAsync<MensajeChatDto>())!;
        Assert.NotNull(enviada.RespuestaA);
        Assert.False(enviada.RespuestaA!.EsMio);                     // para el dueño, la cita es del otro
        Assert.Equal(original.Texto, enviada.RespuestaA.Texto);

        var vista = (await otro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Last();
        Assert.Equal(original.Id, vista.RespuestaA!.Id);
        Assert.True(vista.RespuestaA.EsMio);                         // para quien escribió el original, es suyo

        // No se puede citar un mensaje de otra conversación (ni uno inexistente)
        Assert.Equal(HttpStatusCode.NotFound, (await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = "x", respuestaAId = Guid.NewGuid() })).StatusCode);
        var (duenio2, _, _, _, conv2) = await PrepararAsync();
        Assert.Equal(HttpStatusCode.Created, (await duenio2.PostAsJsonAsync($"/api/v1/conversaciones/{conv2}/mensajes", new { texto = "Hola" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await duenio2.PostAsJsonAsync($"/api/v1/conversaciones/{conv2}/mensajes",
            new { texto = "Cito un chat ajeno", respuestaAId = original.Id })).StatusCode);
    }

    // ---------------- Estados (✓ / ✓✓ / leído), "escribiendo…" y "en línea" ----------------

    private sealed class Oyente : IAsyncDisposable
    {
        public readonly HubConnection Conexion;
        public readonly ConcurrentQueue<(string Evento, JsonElement Datos)> Eventos = new();
        public Oyente(HubConnection c)
        {
            Conexion = c;
            foreach (var e in new[] { "estadoMensajes", "escribiendo", "presencia", "mensaje" })
                c.On<JsonElement>(e, d => Eventos.Enqueue((e, d)));
        }
        public async Task<JsonElement> EsperarAsync(string evento, Func<JsonElement, bool>? filtro = null)
        {
            var limite = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < limite)
            {
                var hallado = Eventos.Where(x => x.Evento == evento && (filtro is null || filtro(x.Datos))).Select(x => (JsonElement?)x.Datos).FirstOrDefault();
                if (hallado is { } h) return h;
                await Task.Delay(50);
            }
            throw new TimeoutException($"No llegó el evento {evento}");
        }
        public ValueTask DisposeAsync() => Conexion.DisposeAsync();
    }

    private async Task<Oyente> ConectarAsync(string token)
    {
        var c = new HubConnectionBuilder()
            .WithUrl(new Uri(_fabrica.Server.BaseAddress, "hubs/notificaciones"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _fabrica.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling;
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            }).Build();
        var oyente = new Oyente(c);
        await c.StartAsync();
        return oyente;
    }

    private async Task<(SesionMinDto Duenio, SesionMinDto Otro, Guid Conv)> PrepararSesionesAsync()
    {
        var duenio = await Api.RegistrarSesionAsync(_fabrica, "duenaEstados");
        var otro = await Api.RegistrarSesionAsync(_fabrica, "otroEstados");
        var pub = await Api.CrearPublicacionAsync(Api.ConToken(_fabrica, duenio.Token));
        var s = await (await Api.ConToken(_fabrica, otro.Token).PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "¿Sigue disponible?" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return (duenio, otro, s.GetProperty("conversacionId").GetGuid());
    }

    [Fact]
    public async Task Enviado_entregado_y_leido_llegan_al_autor_en_tiempo_real()
    {
        var (duenio, otro, conv) = await PrepararSesionesAsync();
        var clienteOtro = Api.ConToken(_fabrica, otro.Token);
        Assert.Equal(EstadoMensajeDto.Enviado, (await clienteOtro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Single().Estado);

        await using var oyenteOtro = await ConectarAsync(otro.Token);
        // La dueña abre la app: lo pendiente le llega al dispositivo (✓✓)
        await using var oyenteDuenio = await ConectarAsync(duenio.Token);
        var entregado = await oyenteOtro.EsperarAsync("estadoMensajes", d => d.GetProperty("conversacionId").GetGuid() == conv);
        Assert.NotEqual(JsonValueKind.Null, entregado.GetProperty("entregadosHastaUtc").ValueKind);
        Assert.Equal(EstadoMensajeDto.Entregado, (await clienteOtro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Single().Estado);

        // …y lo lee
        (await Api.ConToken(_fabrica, duenio.Token).PostAsync($"/api/v1/conversaciones/{conv}/leer", null)).EnsureSuccessStatusCode();
        await oyenteOtro.EsperarAsync("estadoMensajes", d => d.GetProperty("leidosHastaUtc").ValueKind != JsonValueKind.Null);
        var final = (await clienteOtro.GetFromJsonAsync<List<MensajeChatDto>>($"/api/v1/conversaciones/{conv}/mensajes"))!.Single();
        Assert.Equal(EstadoMensajeDto.Leido, final.Estado);
        Assert.True(final.Leido);
    }

    [Fact]
    public async Task Escribiendo_solo_llega_a_la_contraparte_y_un_intruso_no_puede_simularlo()
    {
        var (duenio, otro, conv) = await PrepararSesionesAsync();
        await using var oyenteOtro = await ConectarAsync(otro.Token);
        await using var oyenteDuenio = await ConectarAsync(duenio.Token);

        var intruso = await Api.RegistrarSesionAsync(_fabrica, "intrusoEscribe");
        await using var oyenteIntruso = await ConectarAsync(intruso.Token);
        await oyenteIntruso.Conexion.InvokeAsync("Escribiendo", conv);

        await oyenteDuenio.Conexion.InvokeAsync("Escribiendo", conv);
        var e = await oyenteOtro.EsperarAsync("escribiendo");
        Assert.Equal(conv, e.GetProperty("conversacionId").GetGuid());
        await Task.Delay(300);
        Assert.Single(oyenteOtro.Eventos, x => x.Evento == "escribiendo");          // el del intruso no llegó
        Assert.DoesNotContain(oyenteDuenio.Eventos, x => x.Evento == "escribiendo"); // ni rebota a quien escribe
    }

    [Fact]
    public async Task En_linea_se_avisa_solo_a_quienes_comparten_conversacion()
    {
        var (duenio, otro, conv) = await PrepararSesionesAsync();
        var clienteOtro = Api.ConToken(_fabrica, otro.Token);
        Assert.False((await clienteOtro.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conv).GetProperty("contraparteEnLinea").GetBoolean());

        await using var oyenteOtro = await ConectarAsync(otro.Token);
        var ajeno = await Api.RegistrarSesionAsync(_fabrica, "ajenoPresencia");
        await using var oyenteAjeno = await ConectarAsync(ajeno.Token);

        var oyenteDuenio = await ConectarAsync(duenio.Token);
        var p = await oyenteOtro.EsperarAsync("presencia", d => d.GetProperty("usuarioId").GetGuid() == duenio.Usuario.Id);
        Assert.True(p.GetProperty("enLinea").GetBoolean());
        Assert.True((await clienteOtro.GetFromJsonAsync<JsonElement>("/api/v1/conversaciones")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == conv).GetProperty("contraparteEnLinea").GetBoolean());

        await oyenteDuenio.DisposeAsync();
        await oyenteOtro.EsperarAsync("presencia", d => d.GetProperty("usuarioId").GetGuid() == duenio.Usuario.Id && !d.GetProperty("enLinea").GetBoolean());
        Assert.DoesNotContain(oyenteAjeno.Eventos, x => x.Evento == "presencia");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Mensajes_vacios_son_400(string? texto)
    {
        var (duenio, _, _, _, conv) = await PrepararAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await duenio.PostAsJsonAsync($"/api/v1/conversaciones/{conv}/mensajes", new { texto = new string('a', 1001) })).StatusCode);
    }
}
