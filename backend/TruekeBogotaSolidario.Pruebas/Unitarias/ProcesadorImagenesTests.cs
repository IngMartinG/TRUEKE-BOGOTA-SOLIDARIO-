using System.Buffers.Binary;
using System.Text;
using SkiaSharp;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Las fotos publicadas no deben revelar dónde vive la persona (GPS del EXIF) ni otros metadatos.</summary>
public class ProcesadorImagenesTests
{
    private const string ModeloCelular = "Celular de la persona";

    private static byte[] Codificar(int ancho, int alto, SKEncodedImageFormat formato)
    {
        using var mapa = new SKBitmap(ancho, alto);
        mapa.Erase(new SKColor(30, 120, 60));
        using var datos = mapa.Encode(formato, 90);
        return datos.ToArray();
    }

    /// <summary>JPEG como el de un celular: EXIF con orientación, modelo del equipo y coordenadas GPS (Bogotá).</summary>
    private static byte[] JpegConGps(int ancho, int alto, ushort orientacion = 1) =>
        InsertarExif(Codificar(ancho, alto, SKEncodedImageFormat.Jpeg), orientacion);

    private static byte[] InsertarExif(byte[] jpeg, ushort orientacion)
    {
        var exif = ExifConGps(orientacion);
        var app1 = new byte[4 + exif.Length];
        app1[0] = 0xFF;
        app1[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(app1.AsSpan(2), (ushort)(exif.Length + 2));
        exif.CopyTo(app1, 4);
        // El segmento APP1 (EXIF) va justo después del marcador de inicio (FF D8).
        return [.. jpeg[..2], .. app1, .. jpeg[2..]];
    }

    /// <summary>Bloque EXIF mínimo (TIFF big-endian): IFD0 con Model, Orientation y puntero GPS; IFD GPS con la latitud.</summary>
    private static byte[] ExifConGps(ushort orientacion)
    {
        var modelo = Encoding.ASCII.GetBytes(ModeloCelular + "\0");
        const int ifd0 = 8, ifdGps = ifd0 + 2 + 3 * 12 + 4, latitud = ifdGps + 2 + 2 * 12 + 4, textoModelo = latitud + 24;
        var tiff = new byte[textoModelo + modelo.Length];
        var s = tiff.AsSpan();
        "MM"u8.CopyTo(s);
        BinaryPrimitives.WriteUInt16BigEndian(s[2..], 42);
        BinaryPrimitives.WriteUInt32BigEndian(s[4..], ifd0);

        void Entrada(int pos, ushort etiqueta, ushort tipo, uint cantidad, uint valor) // entrada de IFD: 12 bytes
        {
            var e = tiff.AsSpan(pos);
            BinaryPrimitives.WriteUInt16BigEndian(e, etiqueta);
            BinaryPrimitives.WriteUInt16BigEndian(e[2..], tipo);
            BinaryPrimitives.WriteUInt32BigEndian(e[4..], cantidad);
            BinaryPrimitives.WriteUInt32BigEndian(e[8..], valor);
        }

        BinaryPrimitives.WriteUInt16BigEndian(s[ifd0..], 3);
        Entrada(ifd0 + 2, 0x0110, 2, (uint)modelo.Length, textoModelo);    // Model (ASCII)
        Entrada(ifd0 + 14, 0x0112, 3, 1, (uint)orientacion << 16);          // Orientation (SHORT, alineado a la izquierda)
        Entrada(ifd0 + 26, 0x8825, 4, 1, ifdGps);                           // puntero al IFD GPS

        BinaryPrimitives.WriteUInt16BigEndian(s[ifdGps..], 2);
        Entrada(ifdGps + 2, 0x0001, 2, 2, (uint)'N' << 24);                 // GPSLatitudeRef = "N"
        Entrada(ifdGps + 14, 0x0002, 5, 3, latitud);                        // GPSLatitude (3 RATIONAL)
        uint[] grados = [4, 1, 36, 1, 3512, 100];
        for (var i = 0; i < grados.Length; i++)
            BinaryPrimitives.WriteUInt32BigEndian(s[(latitud + i * 4)..], grados[i]);
        modelo.CopyTo(s[textoModelo..]);

        return [.. "Exif\0\0"u8, .. tiff];
    }

    private static SKCodec Leer(byte[] imagen) => SKCodec.Create(SKData.CreateCopy(imagen))!;

    [Fact]
    public async Task Elimina_el_GPS_y_todos_los_metadatos_EXIF()
    {
        var original = JpegConGps(800, 600, orientacion: 3);
        using (var antes = Leer(original))
            Assert.Equal(SKEncodedOrigin.BottomRight, antes.EncodedOrigin); // la prueba parte de una foto con EXIF legible
        Assert.Contains(ModeloCelular, Encoding.ASCII.GetString(original));

        var limpia = await ProcesadorImagenes.LimpiarAsync(new MemoryStream(original), "jpg");

        using var despues = Leer(limpia);
        Assert.Equal(SKEncodedImageFormat.Jpeg, despues.EncodedFormat);
        Assert.Equal(SKEncodedOrigin.TopLeft, despues.EncodedOrigin);
        var texto = Encoding.ASCII.GetString(limpia);
        Assert.DoesNotContain("Exif", texto);
        Assert.DoesNotContain(ModeloCelular, texto);
    }

    [Fact]
    public async Task Aplica_la_orientacion_antes_de_borrar_el_EXIF_y_reduce_fotos_grandes()
    {
        // Orientación 6 = la cámara guardó la foto "acostada": al aplicarla, el ancho y el alto se intercambian.
        var girada = JpegConGps(3000, 2000, orientacion: 6);
        using var resultado = Leer(await ProcesadorImagenes.LimpiarAsync(new MemoryStream(girada), "jpg"));
        Assert.True(resultado.Info.Height > resultado.Info.Width);
        Assert.Equal(ProcesadorImagenes.LadoMaximo, Math.Max(resultado.Info.Width, resultado.Info.Height));
    }

    [Fact]
    public async Task Gira_los_pixeles_y_no_solo_las_dimensiones()
    {
        // Mitad izquierda roja y derecha azul; con orientación 6 (90° a la derecha) el rojo debe quedar arriba.
        using var mapa = new SKBitmap(200, 100);
        using (var lienzo = new SKCanvas(mapa))
        {
            lienzo.Clear(SKColors.Blue);
            using var rojo = new SKPaint { Color = SKColors.Red };
            lienzo.DrawRect(0, 0, 100, 100, rojo);
        }
        using var jpeg = mapa.Encode(SKEncodedImageFormat.Jpeg, 95);
        var conExif = InsertarExif(jpeg.ToArray(), orientacion: 6);

        using var resultado = SKBitmap.Decode(await ProcesadorImagenes.LimpiarAsync(new MemoryStream(conExif), "png"));
        Assert.Equal(100, resultado.Width);
        Assert.Equal(200, resultado.Height);
        Assert.True(resultado.GetPixel(50, 40).Red > 200, "arriba debe quedar el rojo");
        Assert.True(resultado.GetPixel(50, 160).Blue > 200, "abajo debe quedar el azul");
    }

    [Fact]
    public async Task Conserva_el_formato_PNG()
    {
        var png = Codificar(50, 40, SKEncodedImageFormat.Png);
        using var resultado = Leer(await ProcesadorImagenes.LimpiarAsync(new MemoryStream(png), "png"));
        Assert.Equal(SKEncodedImageFormat.Png, resultado.EncodedFormat);
        Assert.Equal(50, resultado.Info.Width);
    }

    [Fact]
    public async Task Conserva_el_formato_WEBP()
    {
        var webp = Codificar(64, 48, SKEncodedImageFormat.Webp);
        using var resultado = Leer(await ProcesadorImagenes.LimpiarAsync(new MemoryStream(webp), "webp"));
        Assert.Equal(SKEncodedImageFormat.Webp, resultado.EncodedFormat);
        Assert.Equal(64, resultado.Info.Width);
    }

    [Fact]
    public async Task Rechaza_archivos_que_no_son_imagenes_validas()
    {
        var falso = new byte[200];
        new byte[] { 0xFF, 0xD8, 0xFF }.CopyTo(falso, 0); // firma JPEG, contenido basura
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ProcesadorImagenes.LimpiarAsync(new MemoryStream(falso), "jpg"));
    }

    [Fact]
    public async Task Rechaza_formatos_que_no_son_de_foto_aunque_vengan_con_extension_jpg()
    {
        // BMP válido de 2×2 (Skia sí sabe leerlo, pero la plataforma solo acepta JPG, PNG y WEBP).
        var bmp = new byte[70];
        "BM"u8.CopyTo(bmp);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), 54);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), 2);
        BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(26), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bmp.AsSpan(28), 24);
        using (var codec = Leer(bmp))
            Assert.Equal(SKEncodedImageFormat.Bmp, codec.EncodedFormat); // la prueba usa un BMP que Skia reconoce
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ProcesadorImagenes.LimpiarAsync(new MemoryStream(bmp), "jpg"));

        // TIFF: Skia no lo decodifica.
        byte[] tiff = [.. "II*\0"u8, 8, 0, 0, 0, .. new byte[100]];
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ProcesadorImagenes.LimpiarAsync(new MemoryStream(tiff), "jpg"));
    }

    [Fact]
    public async Task Rechaza_extensiones_no_permitidas()
    {
        var png = Codificar(10, 10, SKEncodedImageFormat.Png);
        await Assert.ThrowsAsync<ReglaDeNegocioException>(() => ProcesadorImagenes.LimpiarAsync(new MemoryStream(png), "gif"));
    }
}
