using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class NotificacionesTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public NotificacionesTests(FabricaApi fabrica) => _fabrica = fabrica;

    private async Task<(HubConnection Conexion, ConcurrentQueue<NotificacionDto> Recibidas)> ConectarAsync(string token)
    {
        var recibidas = new ConcurrentQueue<NotificacionDto>();
        var conexion = new HubConnectionBuilder()
            .WithUrl(new Uri(_fabrica.Server.BaseAddress, "hubs/notificaciones"), o =>
            {
                o.HttpMessageHandlerFactory = _ => _fabrica.Server.CreateHandler();
                o.Transports = HttpTransportType.LongPolling; // TestServer no soporta WebSockets
                o.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
        conexion.On<NotificacionDto>("notificacion", n => recibidas.Enqueue(n));
        await conexion.StartAsync();
        return (conexion, recibidas);
    }

    private static async Task<bool> EsperarAsync(Func<bool> condicion, int ms = 5000)
    {
        var limite = DateTime.UtcNow.AddMilliseconds(ms);
        while (DateTime.UtcNow < limite)
        {
            if (condicion()) return true;
            await Task.Delay(50);
        }
        return condicion();
    }

    [Fact]
    public async Task Duenio_recibe_solicitud_nueva_y_solicitante_recibe_aceptada_solo_cada_uno_la_suya()
    {
        var sesDuenio = await Api.RegistrarSesionAsync(_fabrica, "ana");
        var sesOtro = await Api.RegistrarSesionAsync(_fabrica, "beto");
        var sesTercero = await Api.RegistrarSesionAsync(_fabrica, "carla");
        var duenio = Api.ConToken(_fabrica, sesDuenio.Token);
        var otro = Api.ConToken(_fabrica, sesOtro.Token);

        var (cDuenio, nDuenio) = await ConectarAsync(sesDuenio.Token);
        var (cOtro, nOtro) = await ConectarAsync(sesOtro.Token);
        var (cTercero, nTercero) = await ConectarAsync(sesTercero.Token);
        await using var _1 = cDuenio; await using var _2 = cOtro; await using var _3 = cTercero;

        var pub = await Api.CrearPublicacionAsync(duenio);
        var resp = await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Hola" });
        var solicitudId = (await resp.Content.ReadFromJsonAsync<IdDto>())!.Id;

        Assert.True(await EsperarAsync(() => nDuenio.Any(n => n.Tipo == TiposNotificacion.SolicitudNueva && n.RecursoId == solicitudId)));

        (await duenio.PostAsync($"/api/v1/solicitudes/{solicitudId}/aceptar", null)).EnsureSuccessStatusCode();
        Assert.True(await EsperarAsync(() => nOtro.Any(n => n.Tipo == TiposNotificacion.SolicitudAceptada && n.RecursoId == solicitudId)));

        await Task.Delay(300);
        Assert.Empty(nTercero);                                                   // nadie recibe notificaciones ajenas
        Assert.DoesNotContain(nOtro, n => n.Tipo == TiposNotificacion.SolicitudNueva);
    }

    [Fact]
    public async Task Pago_aprobado_se_notifica_al_pagador()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "pagador");
        var cliente = Api.ConToken(_fabrica, ses.Token);
        var (con, recibidas) = await ConectarAsync(ses.Token);
        await using var _ = con;

        var ini = await cliente.PostAsJsonAsync("/api/v1/pagos/iniciar", new { concepto = "Recarga", montoRecargaCop = 10000 });
        Assert.True(ini.StatusCode == HttpStatusCode.Created, await ini.Content.ReadAsStringAsync());
        var referencia = (await ini.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("referencia").GetString();
        (await cliente.PostAsync($"/api/v1/pagos/{referencia}/simular?aprobado=true", null)).EnsureSuccessStatusCode();

        Assert.True(await EsperarAsync(() => recibidas.Any(n => n.Tipo == TiposNotificacion.PagoAprobado)));
    }

    [Fact]
    public async Task Las_notificaciones_quedan_en_la_bandeja_aunque_el_usuario_este_desconectado()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "desconectada");   // sin conexión al hub
        var otro = await Api.RegistrarAsync(_fabrica, "interesado");
        var pub = await Api.CrearPublicacionAsync(duenio);
        (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Hola" })).EnsureSuccessStatusCode();

        Assert.Equal(1, await duenio.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));
        var pagina = await duenio.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/notificaciones?soloNoLeidas=true");
        var n = pagina.GetProperty("items")[0];
        Assert.Equal(TiposNotificacion.SolicitudNueva, n.GetProperty("tipo").GetString());
        Assert.False(n.GetProperty("leida").GetBoolean());
        var id = n.GetProperty("id").GetGuid();

        // nadie más puede marcarla (no existe para otros)
        Assert.Equal(HttpStatusCode.NotFound, (await otro.PostAsync($"/api/v1/notificaciones/{id}/leer", null)).StatusCode);
        Assert.Equal(0, await otro.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));

        Assert.Equal(HttpStatusCode.NoContent, (await duenio.PostAsync($"/api/v1/notificaciones/{id}/leer", null)).StatusCode);
        Assert.Equal(0, await duenio.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));
    }

    [Fact]
    public async Task Marcar_todas_como_leidas()
    {
        var duenio = await Api.RegistrarAsync(_fabrica, "muchas");
        var otro = await Api.RegistrarAsync(_fabrica, "solicita");
        for (var i = 0; i < 3; i++)
        {
            var pub = await Api.CrearPublicacionAsync(duenio);
            (await otro.PostAsJsonAsync("/api/v1/solicitudes", new { publicacionId = pub, mensaje = "Hola" })).EnsureSuccessStatusCode();
        }
        Assert.Equal(3, await duenio.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));
        (await duenio.PostAsync("/api/v1/notificaciones/leer-todas", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, await duenio.GetFromJsonAsync<int>("/api/v1/notificaciones/no-leidas/total"));
    }

    [Fact]
    public async Task Hub_rechaza_conexion_sin_token_o_con_token_invalido()
    {
        var anonimo = _fabrica.CreateClient();
        var sinToken = await anonimo.PostAsync("/hubs/notificaciones/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        var falso = await anonimo.PostAsync("/hubs/notificaciones/negotiate?negotiateVersion=1&access_token=eyJhbGciOiJub25lIn0.e30.", null);
        Assert.Equal(HttpStatusCode.Unauthorized, falso.StatusCode);
    }

    [Fact]
    public async Task Token_por_query_solo_vale_en_la_ruta_del_hub()
    {
        var ses = await Api.RegistrarSesionAsync(_fabrica, "query");
        var anonimo = _fabrica.CreateClient();

        var enHub = await anonimo.PostAsync($"/hubs/notificaciones/negotiate?negotiateVersion=1&access_token={ses.Token}", null);
        Assert.Equal(HttpStatusCode.OK, enHub.StatusCode);

        var enApi = await anonimo.GetAsync($"/api/v1/usuarios/yo?access_token={ses.Token}");
        Assert.Equal(HttpStatusCode.Unauthorized, enApi.StatusCode);
    }
}
