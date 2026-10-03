using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Las fotos publicadas no deben revelar dónde vive la persona (GPS del EXIF) ni otros metadatos.</summary>
public class ProcesadorImagenesTests
{
    private static byte[] JpegConGps(int ancho, int alto, ushort orientacion = 1)
    {
        using var img = new Image<Rgba32>(ancho, alto, new Rgba32(30, 120, 60));
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.GPSLatitude, new[] { new Rational(4, 1), new Rational(36, 1), new Rational(3512, 100) });
        exif.SetValue(ExifTag.GPSLongitudeRef, "W");
        exif.SetValue(ExifTag.GPSLongitude, new[] { new Rational(74, 1), new Rational(4, 1), new Rational(1234, 100) });
        exif.SetValue(ExifTag.Model, "Celular de la persona");
        exif.SetValue(ExifTag.Orientation, orientacion);
        img.Metadata.ExifProfile = exif;
        using var ms = new MemoryStream();
        img.SaveAsJpeg(ms);
        return ms.ToArray();
    }

    [Fact]
    public async Task Elimina_el_GPS_y_todos_los_metadatos_EXIF()
    {
        var original = JpegConGps(800, 600);
        using (var antes = Image.Load(original))
            Assert.True(antes.Metadata.ExifProfile!.TryGetValue(ExifTag.GPSLatitude, out _)); // la prueba parte de una foto con GPS

        var limpia = await ProcesadorImagenes.LimpiarAsync(new MemoryStream(original), "jpg");

        using var despues = Image.Load(limpia);
        Assert.Null(despues.Metadata.ExifProfile);
        Assert.Null(despues.Metadata.XmpProfile);
        Assert.Null(despues.Metadata.IptcProfile);
        Assert.Equal(JpegFormat.Instance, despues.Metadata.DecodedImageFormat);
        Assert.DoesNotContain("Celular de la persona", System.Text.Encoding.ASCII.GetString(limpia));
    }

    [Fact]
    public async Task Aplica_la_orientacion_antes_de_borrar_el_EXIF_y_reduce_fotos_grandes()
    {
        // Orientación 6 = la cámara guardó la foto "acostada": al aplicarla, el ancho y el alto se intercambian.
        var girada = JpegConGps(3000, 2000, orientacion: 6);
        using var resultado = Image.Load(await ProcesadorImagenes.LimpiarAsync(new MemoryStream(girada), "jpg"));
        Assert.True(resultado.Height > resultado.Width);
        Assert.Equal(ProcesadorImagenes.LadoMaximo, Math.Max(resultado.Width, resultado.Height));
    }

    [Fact]
    public async Task Conserva_el_formato_PNG()
    {
        using var img = new Image<Rgba32>(50, 40);
        using var ms = new MemoryStream();
        img.SaveAsPng(ms);
        using var resultado = Image.Load(await ProcesadorImagenes.LimpiarAsync(new MemoryStream(ms.ToArray()), "png"));
        Assert.Equal(PngFormat.Instance, resultado.Metadata.DecodedImageFormat);
        Assert.Equal(50, resultado.Width);
    }

    [Fact]
    public async Task Rechaza_archivos_que_no_son_imagenes_validas()
    {
        var falso = new byte[200];
        new byte[] { 0xFF, 0xD8, 0xFF }.CopyTo(falso, 0); // firma JPEG, contenido basura
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ProcesadorImagenes.LimpiarAsync(new MemoryStream(falso), "jpg"));
    }
}
