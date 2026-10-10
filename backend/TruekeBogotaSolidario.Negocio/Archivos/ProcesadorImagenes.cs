using SkiaSharp;
using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>
/// Limpia las fotos antes de publicarlas. Las cámaras de los celulares guardan en el EXIF las coordenadas GPS exactas
/// donde se tomó la foto (normalmente la casa de la persona): si se publicara el archivo original, cualquiera podría
/// descargarlo y saber dónde vive, aunque la API oculte las coordenadas de la publicación.
/// Aquí se re-codifica la imagen con SkiaSharp: se aplica la orientación, se reduce a un tamaño razonable y se escribe un
/// archivo nuevo a partir de los píxeles, sin ningún metadato del original (EXIF con GPS, IPTC, XMP). También neutraliza
/// contenido "políglota" escondido en el archivo.
/// </summary>
public static class ProcesadorImagenes
{
    /// <summary>Lado mayor máximo de la foto publicada (suficiente para verla en pantalla completa).</summary>
    public const int LadoMaximo = 1600;
    /// <summary>Protección contra "bombas de descompresión": imágenes pequeñas en bytes pero gigantes en píxeles.</summary>
    public const int DimensionMaximaEntrada = 12_000;
    public const long PixelesMaximosEntrada = 60_000_000;

    private const string MensajeInvalida = "La imagen está dañada o no es un formato válido (JPG, PNG o WEBP).";

    public static Task<byte[]> LimpiarAsync(Stream entrada, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        var formato = Formato(extension);
        try
        {
            ct.ThrowIfCancellationRequested();
            using var datos = SKData.Create(entrada) ?? throw new ReglaDeNegocioException(MensajeInvalida);
            // SKCodec solo lee la cabecera: el tamaño se valida antes de reservar memoria para los píxeles.
            using var codec = SKCodec.Create(datos) ?? throw new ReglaDeNegocioException(MensajeInvalida);

            // Skia también decodifica GIF, BMP, ICO, WBMP, etc.: solo se aceptan los formatos de foto de la plataforma.
            if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
                throw new ReglaDeNegocioException(MensajeInvalida);

            var (ancho, alto) = (codec.Info.Width, codec.Info.Height);
            if (ancho <= 0 || alto <= 0 || ancho > DimensionMaximaEntrada || alto > DimensionMaximaEntrada
                || (long)ancho * alto > PixelesMaximosEntrada)
                throw new ReglaDeNegocioException("La imagen es demasiado grande. Usa una foto de máximo 12.000 píxeles por lado.");

            ct.ThrowIfCancellationRequested();
            using var decodificada = Decodificar(codec, ancho, alto);
            ct.ThrowIfCancellationRequested();
            using var final = OrientarYReducir(decodificada, codec.EncodedOrigin);

            using var pixeles = final.PeekPixels();
            using var salida = Codificar(pixeles, formato) ?? throw new ReglaDeNegocioException(MensajeInvalida);
            return Task.FromResult(salida.ToArray());
        }
        catch (Exception ex) when (ex is not ReglaDeNegocioException and not OperationCanceledException and not OutOfMemoryException)
        {
            // El archivo viene de un usuario: cualquier fallo del decodificador (incluso uno inesperado) es "imagen inválida".
            throw new ReglaDeNegocioException(MensajeInvalida);
        }
    }

    /// <summary>
    /// Decodifica solo el primer cuadro (un WEBP animado queda como foto fija), en sRGB. Si la foto es más grande de lo
    /// que se va a publicar, el decodificador JPEG puede entregarla ya reducida (1/2, 1/4, 1/8): usa mucha menos memoria.
    /// </summary>
    private static SKBitmap Decodificar(SKCodec codec, int ancho, int alto)
    {
        var tamano = new SKSizeI(ancho, alto);
        var escala = (float)LadoMaximo / Math.Max(ancho, alto);
        if (escala < 1f)
        {
            var reducido = codec.GetScaledDimensions(escala);
            // Solo si no queda por debajo del tamaño final (si no, la foto publicada perdería nitidez).
            if (Math.Max(reducido.Width, reducido.Height) >= LadoMaximo)
                tamano = reducido;
        }

        var info = new SKImageInfo(tamano.Width, tamano.Height, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var mapa = new SKBitmap(info);
        try
        {
            if (codec.GetPixels(info, mapa.GetPixels()) != SKCodecResult.Success)
                throw new ReglaDeNegocioException(MensajeInvalida);
            return mapa;
        }
        catch
        {
            mapa.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Aplica la orientación del EXIF (si no, las fotos verticales del celular quedarían de lado) y reduce el lado mayor
    /// a <see cref="LadoMaximo"/>, todo en un solo dibujo con remuestreo de buena calidad.
    /// </summary>
    private static SKBitmap OrientarYReducir(SKBitmap origen, SKEncodedOrigin orientacion)
    {
        var (w, h) = (origen.Width, origen.Height);
        var gira = orientacion is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (anchoOrientado, altoOrientado) = gira ? (h, w) : (w, h);

        var escala = Math.Min(1.0, (double)LadoMaximo / Math.Max(anchoOrientado, altoOrientado));
        var anchoFinal = Math.Max(1, (int)Math.Round(anchoOrientado * escala));
        var altoFinal = Math.Max(1, (int)Math.Round(altoOrientado * escala));

        var destino = new SKBitmap(new SKImageInfo(anchoFinal, altoFinal, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb()));
        using var lienzo = new SKCanvas(destino);
        lienzo.Clear(SKColors.Transparent);
        lienzo.Scale((float)anchoFinal / anchoOrientado, (float)altoFinal / altoOrientado);
        lienzo.Concat(MatrizOrientacion(orientacion, w, h));
        using var imagen = SKImage.FromBitmap(origen);
        lienzo.DrawImage(imagen, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
        lienzo.Flush();
        return destino;
    }

    /// <summary>Transformación que lleva la imagen guardada (w × h) a como debe verse, según la etiqueta Orientation del EXIF.</summary>
    private static SKMatrix MatrizOrientacion(SKEncodedOrigin orientacion, int w, int h) => orientacion switch
    {
        // x' = scaleX·x + skewX·y + transX ; y' = skewY·x + scaleY·y + transY
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),     // espejo horizontal
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1), // 180°
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),   // espejo vertical
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),       // transpuesta
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),     // 90° a la derecha (la más común)
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1), // transversa
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),   // 90° a la izquierda
        _ => SKMatrix.Identity
    };

    private static SKEncodedImageFormat Formato(string extension) => extension switch
    {
        "jpg" => SKEncodedImageFormat.Jpeg,
        "png" => SKEncodedImageFormat.Png,
        "webp" => SKEncodedImageFormat.Webp,
        _ => throw new ReglaDeNegocioException("La imagen debe ser JPG, PNG o WEBP.")
    };

    private static SKData? Codificar(SKPixmap pixeles, SKEncodedImageFormat formato) => formato switch
    {
        SKEncodedImageFormat.Jpeg => pixeles.Encode(new SKJpegEncoderOptions(82, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore)),
        SKEncodedImageFormat.Png => pixeles.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, zLibLevel: 9)),
        _ => pixeles.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossy, 80))
    };
}
