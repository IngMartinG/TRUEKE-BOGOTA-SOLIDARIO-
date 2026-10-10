using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>Vista previa al compartir (WhatsApp, Facebook, X): etiquetas Open Graph sin datos que no sean públicos.</summary>
public class CompartirTests : IClassFixture<FabricaApi>
{
    private readonly AlmacenFalso _almacen = new();
    private readonly WebApplicationFactory<Program> _f;

    public CompartirTests(FabricaApi fabrica)
        => _f = fabrica.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.Replace(ServiceDescriptor.Singleton<IAlmacenArchivos>(_almacen))));

    private static byte[] FotoReal(int ancho, int alto)
    {
        using var mapa = new SKBitmap(ancho, alto);
        mapa.Erase(new SKColor(30, 120, 60));
        using var datos = mapa.Encode(SKEncodedImageFormat.Png, 90);
        return datos.ToArray();
    }

    private async Task<(HttpClient Cliente, Guid Id)> PublicarConFotoAsync(string titulo)
    {
        var c = await Api.RegistrarAsync(_f, "comparte");
        var r = await c.PostAsJsonAsync("/api/v1/archivos/subidas", new { tipo = "Imagen", contentType = "image/png", tamanoBytes = 1000 });
        var subida = (await r.Content.ReadFromJsonAsync<SubidaArchivoDto>())!;
        _almacen.Subir(subida.UrlArchivo, "image/png", FotoReal(800, 800));
        var pub = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo, descripcion = "Funciona perfecto.\nIncluye control.", categoriaId = 4, modo = "Compra", precioReferenciaCop = 50000,
            condicion = "Usado", localidad = "Kennedy", imagenes = new[] { subida.UrlArchivo }
        });
        Assert.True(pub.StatusCode == HttpStatusCode.Created, await pub.Content.ReadAsStringAsync());
        return (c, (await pub.Content.ReadFromJsonAsync<IdDto>())!.Id);
    }

    [Fact]
    public async Task La_pagina_tiene_etiquetas_Open_Graph_escapadas_y_redirige_al_front()
    {
        var (_, id) = await PublicarConFotoAsync("Televisor <b>\"32\"</b>");
        var r = await _f.CreateClient().GetAsync($"/compartir/publicaciones/{id}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/html", r.Content.Headers.ContentType!.MediaType);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Contains("<meta property=\"og:title\" content=\"Televisor &lt;b&gt;&quot;32&quot;&lt;/b&gt;\">", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("Compra · $50.000 · Kennedy", html);
        Assert.Contains("Funciona perfecto. Incluye control.", html);  // una sola línea
        Assert.Contains($"/compartir/publicaciones/{id}/imagen.jpg\"", html);
        Assert.Matches("og:image\" content=\"https?://", html);         // absoluta: WhatsApp no resuelve rutas relativas
        Assert.Contains($"/publicacion/{id}\"", html);                    // redirección a la publicación en el front
        Assert.Contains("summary_large_image", html);
    }

    [Fact]
    public async Task La_imagen_es_un_JPEG_de_1200x630()
    {
        var (_, id) = await PublicarConFotoAsync("Bicicleta para compartir");
        var r = await _f.CreateClient().GetAsync($"/compartir/publicaciones/{id}/imagen.jpg");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("image/jpeg", r.Content.Headers.ContentType!.MediaType);
        using var codec = SKCodec.Create(SKData.CreateCopy(await r.Content.ReadAsByteArrayAsync()));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal((1200, 630), (codec.Info.Width, codec.Info.Height));
    }

    [Fact]
    public async Task Lo_que_no_es_publico_muestra_la_vista_general_sin_revelar_nada()
    {
        var (c, id) = await PublicarConFotoAsync("Secreto cancelado");
        (await c.PostAsJsonAsync($"/api/v1/publicaciones/{id}/cancelar", new { motivo = "Ya no" })).EnsureSuccessStatusCode();
        var anonimo = _f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var cancelada = await (await anonimo.GetAsync($"/compartir/publicaciones/{id}")).Content.ReadAsStringAsync();
        var inexistente = await (await anonimo.GetAsync($"/compartir/publicaciones/{Guid.NewGuid()}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Secreto", cancelada);
        Assert.Contains("og-imagen.png", cancelada);
        Assert.Contains("og-imagen.png", inexistente);

        var imagen = await anonimo.GetAsync($"/compartir/publicaciones/{id}/imagen.jpg");
        Assert.Equal(HttpStatusCode.Redirect, imagen.StatusCode);
        Assert.EndsWith("/og-imagen.png", imagen.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("Corta", "Corta")]
    [InlineData("  varias\n\nlíneas   y espacios ", "varias líneas y espacios")]
    public void Resume_la_descripcion_en_una_linea(string entrada, string esperado)
        => Assert.Equal(esperado, VistaPreviaService.Resumir(entrada));

    [Fact]
    public void Corta_las_descripciones_largas_en_un_espacio()
    {
        var resumen = VistaPreviaService.Resumir(string.Join(' ', Enumerable.Repeat("palabra", 60)));
        Assert.True(resumen.Length <= VistaPreviaService.LongitudDescripcion + 1);
        Assert.EndsWith("palabra…", resumen);
    }
}
