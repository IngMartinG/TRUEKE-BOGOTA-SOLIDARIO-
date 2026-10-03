using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Archivos;

/// <summary>
/// Limpia las fotos antes de publicarlas. Las cámaras de los celulares guardan en el EXIF las coordenadas GPS exactas
/// donde se tomó la foto (normalmente la casa de la persona): si se publicara el archivo original, cualquiera podría
/// descargarlo y saber dónde vive, aunque la API oculte las coordenadas de la publicación.
/// Aquí se re-codifica la imagen: se aplica la orientación, se reduce a un tamaño razonable y se descartan TODOS los
/// metadatos (EXIF con GPS, IPTC, XMP). También neutraliza contenido "políglota" escondido en el archivo.
/// </summary>
public static class ProcesadorImagenes
{
    /// <summary>Lado mayor máximo de la foto publicada (suficiente para verla en pantalla completa).</summary>
    public const int LadoMaximo = 1600;
    /// <summary>Protección contra "bombas de descompresión": imágenes pequeñas en bytes pero gigantes en píxeles.</summary>
    public const int DimensionMaximaEntrada = 12_000;
    public const long PixelesMaximosEntrada = 60_000_000;

    public static async Task<byte[]> LimpiarAsync(Stream entrada, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        try
        {
            var info = await Image.IdentifyAsync(entrada, ct); // solo lee la cabecera
            if (info.Width <= 0 || info.Height <= 0 || info.Width > DimensionMaximaEntrada || info.Height > DimensionMaximaEntrada
                || (long)info.Width * info.Height > PixelesMaximosEntrada)
                throw new ReglaDeNegocioException("La imagen es demasiado grande. Usa una foto de máximo 12.000 píxeles por lado.");

            entrada.Position = 0;
            using var imagen = await Image.LoadAsync(new DecoderOptions { MaxFrames = 1 }, entrada, ct);
            imagen.Mutate(x =>
            {
                x.AutoOrient(); // usa la orientación del EXIF ANTES de borrarlo (si no, las fotos verticales quedarían de lado)
                if (imagen.Width > LadoMaximo || imagen.Height > LadoMaximo)
                    x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(LadoMaximo, LadoMaximo) });
            });
            QuitarMetadatos(imagen);

            using var salida = new MemoryStream();
            await imagen.SaveAsync(salida, Codificador(extension), ct);
            return salida.ToArray();
        }
        catch (Exception ex) when (ex is not ReglaDeNegocioException and not OperationCanceledException and not OutOfMemoryException)
        {
            // El archivo viene de un usuario: cualquier fallo del decodificador (incluso uno inesperado) es "imagen inválida".
            throw new ReglaDeNegocioException("La imagen está dañada o no es un formato válido (JPG, PNG o WEBP).");
        }
    }

    private static void QuitarMetadatos(Image imagen)
    {
        imagen.Metadata.ExifProfile = null;
        imagen.Metadata.IptcProfile = null;
        imagen.Metadata.XmpProfile = null;
        foreach (var cuadro in imagen.Frames)
        {
            cuadro.Metadata.ExifProfile = null;
            cuadro.Metadata.IptcProfile = null;
            cuadro.Metadata.XmpProfile = null;
        }
    }

    private static IImageEncoder Codificador(string extension) => extension switch
    {
        "jpg" => new JpegEncoder { Quality = 82 },
        "png" => new PngEncoder { CompressionLevel = PngCompressionLevel.BestCompression },
        "webp" => new WebpEncoder { Quality = 80 },
        _ => throw new ReglaDeNegocioException("La imagen debe ser JPG, PNG o WEBP.")
    };
}
