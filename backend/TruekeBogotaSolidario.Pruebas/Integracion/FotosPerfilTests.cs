using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// Foto de perfil real: sin rostro se rechaza al subirla; dudosa queda en revisión para un moderador; las fotos anteriores a la
/// regla se revisan en segundo plano y, si no muestran un rostro, la persona tiene 7 días para cambiarla antes de quedar
/// limitada a explorar.
/// </summary>
public sealed class FotosPerfilTests : IClassFixture<FabricaApi>, IDisposable
{
    private readonly FabricaApi _fabrica;
    private readonly AlmacenFalso _almacen = new();
    private readonly RelojFalso _reloj = new();
    private readonly WebApplicationFactory<Program> _f;

    public FotosPerfilTests(FabricaApi fabrica)
    {
        _fabrica = fabrica;
        _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen));
            s.Replace(ServiceDescriptor.Singleton<TimeProvider>(_reloj));
        }));
    }

    public void Dispose()
    {
        _fabrica.Detector.Resultado = DetectorRostrosFalso.CaraClara;
        _fabrica.Detector.PorImagen = null;
    }

    private async Task<HttpResponseMessage> SubirFotoAsync(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "Imagen", contentType = "image/png", tamanoBytes = 100 });
        var s = (await r.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
        _almacen.Subir(s.UrlArchivo, "image/png", AlmacenFalso.Png());
        return await c.PutAsJsonAsync("/api/v1/usuarios/yo/foto", new { url = s.UrlArchivo });
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient c)
        => c.PostAsJsonAsync("/api/v1/publicaciones", new { titulo = "Lámpara", descripcion = "Funciona", categoriaId = 4, modo = "Trueke", condicion = "ComoNuevo", localidad = "Kennedy" });

    private async Task<HttpClient> ModeradorAsync() => Api.ConToken(_f, await Api.LoginAsync(_f, FabricaApi.CorreoSuper, FabricaApi.ClaveSuper));

    private async Task<string[]> TiposAvisosAsync(HttpClient c)
        => (await c.GetFromJsonAsync<JsonElement>("/api/v1/notificaciones")).GetProperty("items").EnumerateArray().Select(n => n.GetProperty("tipo").GetString()!).ToArray();

    [Fact]
    public async Task Una_foto_sin_rostro_se_rechaza_al_subirla_y_no_se_conserva()
    {
        var c = await Api.RegistrarAsync(_f, "sinCara");
        var fotoAntes = (await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("fotoUrl").GetString();
        _fabrica.Detector.Resultado = DetectorRostrosFalso.SinRostro;
        var antes = _almacen.Urls().Count(u => u.Contains("/imagenes/"));

        var r = await SubirFotoAsync(c);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("No encontramos un rostro", await r.Content.ReadAsStringAsync());
        Assert.Equal(antes, _almacen.Urls().Count(u => u.Contains("/imagenes/")));
        var yo = await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo");
        Assert.Equal(fotoAntes, yo.GetProperty("fotoUrl").GetString()); // se conserva la que tenía
    }

    [Fact]
    public async Task Una_foto_clara_queda_aprobada()
    {
        var c = await Api.RegistrarAsync(_f, "conCara");
        var yo = await (await SubirFotoAsync(c)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobada", yo.GetProperty("estadoFoto").GetString());
        Assert.True(yo.GetProperty("fotoValida").GetBoolean());
    }

    [Fact]
    public async Task Una_foto_dudosa_queda_en_revision_sin_bloquear_y_un_moderador_la_aprueba()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "dudosa");
        var c = Api.ConToken(_f, ses.Token);
        _fabrica.Detector.Resultado = DetectorRostrosFalso.Dudosa;
        var yo = await (await SubirFotoAsync(c)).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EnRevision", yo.GetProperty("estadoFoto").GetString());
        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(c)).StatusCode); // mientras tanto puede usar la cuenta

        var mod = await ModeradorAsync();
        var cola = await mod.GetFromJsonAsync<FotosEnRevisionDto>("/api/v1/admin/fotos");
        Assert.Contains(cola!.Items, i => i.UsuarioId == ses.Usuario.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await mod.PostAsync($"/api/v1/admin/fotos/{ses.Usuario.Id}/aprobar", null)).StatusCode);
        Assert.Equal("Aprobada", (await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("estadoFoto").GetString());
        Assert.Contains("FotoPerfilAprobada", await TiposAvisosAsync(c));
        cola = await mod.GetFromJsonAsync<FotosEnRevisionDto>("/api/v1/admin/fotos");
        Assert.DoesNotContain(cola!.Items, i => i.UsuarioId == ses.Usuario.Id);
    }

    [Fact]
    public async Task Un_cliente_no_ve_la_cola_de_fotos()
    {
        var c = await Api.RegistrarAsync(_f, "curioso");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/admin/fotos")).StatusCode);
    }

    [Fact]
    public async Task Rechazada_por_un_moderador_puede_seguir_hasta_el_plazo_y_despues_debe_cambiarla()
    {
        var ses = await Api.RegistrarSesionAsync(_f, "rechazada");
        var c = Api.ConToken(_f, ses.Token);
        await SubirFotoAsync(c);
        var mod = await ModeradorAsync();
        Assert.Equal(HttpStatusCode.NoContent,
            (await mod.PostAsJsonAsync($"/api/v1/admin/fotos/{ses.Usuario.Id}/rechazar", new { motivo = "La foto es un paisaje." })).StatusCode);

        var yo = await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo");
        Assert.Equal("Rechazada", yo.GetProperty("estadoFoto").GetString());
        Assert.Equal("La foto es un paisaje.", yo.GetProperty("motivoRechazoFoto").GetString());
        Assert.NotEqual(JsonValueKind.Null, yo.GetProperty("plazoCambioFotoUtc").ValueKind);
        Assert.Contains("FotoPerfilRechazada", await TiposAvisosAsync(c));
        Assert.Contains(_fabrica.Correos.Para(ses.Usuario.Correo), m => m.Asunto == "Cambia tu foto de perfil");
        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(c)).StatusCode); // aún dentro del plazo

        _reloj.Avanzar(TimeSpan.FromDays(8));
        var r = await PublicarAsync(c);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("foto_requerida", await r.Content.ReadAsStringAsync());
        Assert.False((await c.GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo")).GetProperty("fotoValida").GetBoolean());

        // Al subir una foto con su cara vuelve a quedar habilitada
        await SubirFotoAsync(c);
        Assert.Equal(HttpStatusCode.Created, (await PublicarAsync(c)).StatusCode);
    }

    [Fact]
    public async Task Las_fotos_anteriores_a_la_regla_se_revisan_en_segundo_plano()
    {
        var conCara = await Api.RegistrarSesionAsync(_f, "viejaCara");
        var sinCara = await Api.RegistrarSesionAsync(_f, "viejaLogo");
        var sinArchivo = await Api.RegistrarSesionAsync(_f, "viejaPerdida");
        string urlCara, urlLogo;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
            urlCara = await _almacen.GuardarImagenAsync(conCara.Usuario.Id, AlmacenFalso.Png());
            urlLogo = await _almacen.GuardarImagenAsync(sinCara.Usuario.Id, AlmacenFalso.Png(101));
            (await db.Usuarios.FindAsync(conCara.Usuario.Id))!.CambiarFoto(urlCara);
            (await db.Usuarios.FindAsync(sinCara.Usuario.Id))!.CambiarFoto(urlLogo);
            (await db.Usuarios.FindAsync(sinArchivo.Usuario.Id))!.CambiarFoto($"{AlmacenFalso.Base}imagenes/{sinArchivo.Usuario.Id:N}/no-existe.jpg");
            await db.SaveChangesAsync();
        }
        // Se decide por imagen: 100 bytes = cara clara, 101 = sin rostro.
        _fabrica.Detector.PorImagen = imagen => imagen.Length == 101 ? DetectorRostrosFalso.SinRostro : DetectorRostrosFalso.CaraClara;

        using (var scope = _f.Services.CreateScope())
        {
            var servicio = scope.ServiceProvider.GetRequiredService<IFotoPerfilService>();
            while (await servicio.RevisarFotosAnterioresAsync(50) == 50) { }
        }

        using var lectura = _f.Services.CreateScope();
        var bd = lectura.ServiceProvider.GetRequiredService<TruekeDbContext>();
        Assert.Equal(EstadoFotoPerfil.Aprobada, (await bd.Usuarios.FindAsync(conCara.Usuario.Id))!.EstadoFoto);
        var rechazada = (await bd.Usuarios.FindAsync(sinCara.Usuario.Id))!;
        Assert.Equal(EstadoFotoPerfil.Rechazada, rechazada.EstadoFoto);
        Assert.Equal(_reloj.GetUtcNow().UtcDateTime.AddDays(7), rechazada.PlazoCambioFotoUtc);
        // La que no se pudo leer queda sin revisar (se reintenta otro día) y no bloquea a nadie
        var perdida = (await bd.Usuarios.FindAsync(sinArchivo.Usuario.Id))!;
        Assert.Equal(EstadoFotoPerfil.SinRevisar, perdida.EstadoFoto);
        Assert.NotNull(perdida.FotoRevisadaUtc);
        Assert.Contains("FotoPerfilRechazada", await TiposAvisosAsync(Api.ConToken(_f, sinCara.Token)));
    }

    [Fact]
    public async Task Denunciar_la_foto_de_una_persona_y_ocultarla_la_rechaza_con_plazo()
    {
        var denunciado = await Api.RegistrarSesionAsync(_f, "fotoAjena");
        await SubirFotoAsync(Api.ConToken(_f, denunciado.Token));
        var denunciante = await Api.RegistrarAsync(_f, "vecino");
        var r = await denunciante.PostAsJsonAsync("/api/v1/denuncias",
            new { tipo = "Usuario", objetivoId = denunciado.Usuario.Id, motivo = "FotoPerfilNoValida" });
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());

        var mod = await ModeradorAsync();
        var id = (await mod.GetFromJsonAsync<JsonElement>("/api/v1/admin/denuncias?estado=Pendiente")).EnumerateArray()
            .Single(g => g.GetProperty("objetivoId").GetGuid() == denunciado.Usuario.Id).GetProperty("denunciaId").GetGuid();
        var resuelta = await mod.PostAsJsonAsync($"/api/v1/admin/denuncias/{id}/resolver",
            new { accion = "OcultarContenido", nota = "La foto es una caricatura." });
        Assert.Equal(HttpStatusCode.NoContent, resuelta.StatusCode);

        var yo = await Api.ConToken(_f, denunciado.Token).GetFromJsonAsync<JsonElement>("/api/v1/usuarios/yo");
        Assert.Equal("Rechazada", yo.GetProperty("estadoFoto").GetString());
        Assert.Contains("FotoPerfilRechazada", await TiposAvisosAsync(Api.ConToken(_f, denunciado.Token)));
    }

    [Fact]
    public async Task El_motivo_de_foto_solo_sirve_para_denunciar_a_una_persona()
    {
        var autor = await Api.RegistrarAsync(_f, "autorPub");
        var pub = (await (await PublicarAsync(autor)).Content.ReadFromJsonAsync<IdDto>())!.Id;
        var c = await Api.RegistrarAsync(_f, "motivo");
        var r = await c.PostAsJsonAsync("/api/v1/denuncias", new { tipo = "Publicacion", objetivoId = pub, motivo = "FotoPerfilNoValida" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}
