using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Negocio.Archivos;

public sealed class FotosPerfilOpciones
{
    public const string Seccion = "FotosPerfil";
    /// <summary>false = no se revisa ninguna foto (quedan "sin revisar" y nadie se bloquea).</summary>
    public bool DetectorHabilitado { get; set; } = true;
    /// <summary>Revisar en segundo plano las fotos anteriores a la regla.</summary>
    public bool RevisarExistentes { get; set; } = true;
    /// <summary>Días para cambiar una foto rechazada antes de que cuente como "sin foto".</summary>
    [Range(1, 60)] public int DiasPlazoCambio { get; set; } = 7;
    /// <summary>Fotos por lote del proceso en segundo plano (con una pausa entre lotes para no cargar el servidor).</summary>
    [Range(1, 500)] public int TamanoLote { get; set; } = 25;
}

/// <summary>
/// Un rostro encontrado: confianza del modelo (0–1), fracción de la imagen que ocupa su recuadro (0–1) y si sus rasgos (ojos,
/// nariz y boca) están donde estarían en una cara de verdad.
/// </summary>
public sealed record RostroDetectado(float Confianza, float Area, bool RasgosCoherentes = true);

public interface IDetectorRostros
{
    /// <summary>Rostros de la imagen (JPEG, PNG o WEBP ya limpia). null si el detector no está disponible o no pudo leerla.</summary>
    IReadOnlyList<RostroDetectado>? Detectar(byte[] imagen);
}

/// <summary>
/// Decide qué pasa con una foto de perfil según los rostros encontrados. Va aparte del modelo para poder probar la regla.
/// </summary>
public static class ReglaFotoPerfil
{
    /// <summary>Por debajo de esta confianza no se cuenta como rostro.</summary>
    public const float ConfianzaMinima = 0.7f;
    /// <summary>Un rostro claro: el modelo está seguro, sus rasgos son coherentes…</summary>
    public const float ConfianzaClara = 0.9f;
    /// <summary>…y la cara ocupa al menos el 1,5 % de la foto (en un retrato de medio cuerpo ocupa ~3 %; menos es alguien lejano).</summary>
    public const float AreaMinima = 0.015f;
    /// <summary>Otras caras al fondo se toleran si son mucho más pequeñas que la principal.</summary>
    public const float ProporcionFondo = 0.25f;

    public const string MensajeSinRostro =
        "No encontramos un rostro en la foto. Sube una foto tuya donde se vea tu cara de frente, sin logos, dibujos ni paisajes.";

    /// <summary>null = no hay rostro (se rechaza en el momento); Aprobada si hay una cara clara; EnRevision si hay dudas.</summary>
    public static EstadoFotoPerfil? Clasificar(IReadOnlyList<RostroDetectado> rostros)
    {
        var validos = rostros.Where(r => r.Confianza >= ConfianzaMinima).OrderByDescending(r => r.Area).ToList();
        if (validos.Count == 0) return null;
        var principal = validos[0];
        var clara = principal.Confianza >= ConfianzaClara && principal.RasgosCoherentes && principal.Area >= AreaMinima
                    && validos.Skip(1).All(r => r.Area < principal.Area * ProporcionFondo);
        return clara ? EstadoFotoPerfil.Aprobada : EstadoFotoPerfil.EnRevision;
    }
}

/// <summary>
/// Detector de rostros que corre en el propio servidor (sin enviar fotos a terceros) con el modelo YuNet de OpenCV Zoo
/// (licencia MIT, 230 KB). Además del recuadro entrega 5 puntos (ojos, nariz y comisuras de la boca), con los que se descartan
/// formas que solo se parecen a una cara (un logo redondo, por ejemplo). Solo detecta que hay una cara: no reconoce a nadie
/// ni guarda rasgos. El modelo se carga la primera vez que se usa; si el entorno no puede cargarlo, la foto queda "sin revisar".
/// </summary>
public sealed class DetectorRostrosOnnx : IDetectorRostros, IDisposable
{
    private const string RecursoModelo = "TruekeBogotaSolidario.Negocio.Archivos.Modelos.face_detection_yunet_2023mar.onnx";
    private const int Lado = 640; // entrada fija del modelo
    private static readonly int[] Pasos = [8, 16, 32];
    private const float UmbralSolape = 0.3f;
    /// <summary>Las fotos publicadas ya vienen reducidas a 1600 px; esto solo protege contra lo inesperado.</summary>
    private const int LadoMaximoEntrada = 4000;

    private readonly ILogger<DetectorRostrosOnnx> _log;
    private readonly bool _habilitado;
    private readonly Lazy<InferenceSession?> _sesion;

    public DetectorRostrosOnnx(IOptions<FotosPerfilOpciones> opciones, ILogger<DetectorRostrosOnnx> log)
    {
        _log = log;
        _habilitado = opciones.Value.DetectorHabilitado;
        _sesion = new Lazy<InferenceSession?>(Cargar, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private InferenceSession? Cargar()
    {
        try
        {
            using var recurso = typeof(DetectorRostrosOnnx).Assembly.GetManifestResourceStream(RecursoModelo)
                                ?? throw new InvalidOperationException("No se encontró el modelo de detección de rostros.");
            using var memoria = new MemoryStream();
            recurso.CopyTo(memoria);
            // Un hilo: el servidor gratis tiene poca CPU y la detección de una foto tarda milisegundos.
            using var opciones = new SessionOptions { IntraOpNumThreads = 1, InterOpNumThreads = 1 };
            return new InferenceSession(memoria.ToArray(), opciones);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudo cargar el detector de rostros: las fotos de perfil quedarán sin revisar");
            return null;
        }
    }

    private readonly record struct Candidato(float Confianza, float X1, float Y1, float X2, float Y2, bool Rasgos);

    public IReadOnlyList<RostroDetectado>? Detectar(byte[] imagen)
    {
        if (!_habilitado || _sesion.Value is not { } sesion) return null;
        DenseTensor<float> entrada;
        float anchoUtil, altoUtil;
        try
        {
            (entrada, anchoUtil, altoUtil) = Preparar(imagen);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.LogInformation(ex, "El detector de rostros no pudo leer una imagen");
            return null;
        }

        using var resultados = sesion.Run([NamedOnnxValue.CreateFromTensor(sesion.InputMetadata.Keys.First(), entrada)]);
        var salidas = resultados.ToDictionary(r => r.Name, r => r.AsTensor<float>());

        var candidatos = new List<Candidato>();
        foreach (var paso in Pasos)
        {
            var (cls, obj, caja, puntos) = (salidas[$"cls_{paso}"], salidas[$"obj_{paso}"], salidas[$"bbox_{paso}"], salidas[$"kps_{paso}"]);
            var columnas = Lado / paso;
            for (var i = 0; i < cls.Dimensions[1]; i++)
            {
                var confianza = MathF.Sqrt(Math.Clamp(cls[0, i, 0], 0f, 1f) * Math.Clamp(obj[0, i, 0], 0f, 1f));
                if (confianza < ReglaFotoPerfil.ConfianzaMinima) continue;
                var (fila, columna) = (i / columnas, i % columnas);
                var cx = (columna + caja[0, i, 0]) * paso;
                var cy = (fila + caja[0, i, 1]) * paso;
                var w = MathF.Exp(caja[0, i, 2]) * paso;
                var h = MathF.Exp(caja[0, i, 3]) * paso;
                var (x1, y1, x2, y2) = (cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2);
                var p = new (float X, float Y)[5];
                for (var k = 0; k < 5; k++) p[k] = ((columna + puntos[0, i, 2 * k]) * paso, (fila + puntos[0, i, 2 * k + 1]) * paso);
                candidatos.Add(new Candidato(confianza, x1, y1, x2, y2, RasgosCoherentes(p, x1, y1, x2, y2)));
            }
        }

        // Supresión de no máximos: un mismo rostro produce varias cajas casi iguales.
        var elegidos = new List<Candidato>();
        foreach (var c in candidatos.OrderByDescending(c => c.Confianza))
            if (elegidos.All(e => Solape(e, c) < UmbralSolape)) elegidos.Add(c);

        var areaUtil = anchoUtil * altoUtil;
        return elegidos.Select(e =>
        {
            var ancho = Math.Max(0, Math.Min(e.X2, anchoUtil) - Math.Max(e.X1, 0));
            var alto = Math.Max(0, Math.Min(e.Y2, altoUtil) - Math.Max(e.Y1, 0));
            return new RostroDetectado(e.Confianza, ancho * alto / areaUtil, e.Rasgos);
        }).ToList();
    }

    /// <summary>
    /// Ojo derecho, ojo izquierdo, nariz, comisura derecha y comisura izquierda deben estar dentro de la cara y en orden: ojos
    /// arriba y separados, nariz entre los ojos y por debajo, boca por debajo de la nariz.
    /// </summary>
    internal static bool RasgosCoherentes((float X, float Y)[] p, float x1, float y1, float x2, float y2)
    {
        var (w, h) = (x2 - x1, y2 - y1);
        if (w <= 0 || h <= 0) return false;
        var margen = 0.1f * w;
        if (p.Any(q => q.X < x1 - margen || q.X > x2 + margen || q.Y < y1 - margen || q.Y > y2 + margen)) return false;
        var (ojoD, ojoI, nariz, bocaD, bocaI) = (p[0], p[1], p[2], p[3], p[4]);
        var ojosY = (ojoD.Y + ojoI.Y) / 2;
        var bocaY = (bocaD.Y + bocaI.Y) / 2;
        var separacionOjos = MathF.Abs(ojoI.X - ojoD.X);
        return separacionOjos >= 0.2f * w
               && ojosY < nariz.Y && nariz.Y < bocaY
               && nariz.X > Math.Min(ojoD.X, ojoI.X) - 0.15f * w && nariz.X < Math.Max(ojoD.X, ojoI.X) + 0.15f * w
               && bocaY - ojosY >= 0.2f * h;
    }

    /// <summary>
    /// Imagen → tensor [1, 3, 640, 640] en BGR con valores 0–255 (como lo espera YuNet). Se reduce sin deformar y se rellena
    /// abajo y a la derecha; devuelve el ancho y el alto que ocupa la foto dentro del cuadro.
    /// </summary>
    private static (DenseTensor<float> Tensor, float Ancho, float Alto) Preparar(byte[] imagen)
    {
        using var codec = SKCodec.Create(new MemoryStream(imagen)) ?? throw new InvalidDataException("Imagen no válida.");
        if (codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > LadoMaximoEntrada || codec.Info.Height > LadoMaximoEntrada)
            throw new InvalidDataException("Tamaño de imagen no admitido.");
        using var original = SKBitmap.Decode(codec) ?? throw new InvalidDataException("Imagen no válida.");
        var escala = Math.Min((float)Lado / original.Width, (float)Lado / original.Height);
        var (ancho, alto) = (Math.Max(1, (int)(original.Width * escala)), Math.Max(1, (int)(original.Height * escala)));
        using var reducida = original.Resize(new SKImageInfo(ancho, alto, SKColorType.Rgba8888, SKAlphaType.Unpremul),
            new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new InvalidDataException("Imagen no válida.");

        var tensor = new DenseTensor<float>([1, 3, Lado, Lado]); // el relleno queda en negro (0)
        for (var y = 0; y < alto; y++)
        for (var x = 0; x < ancho; x++)
        {
            var c = reducida.GetPixel(x, y);
            // Lo transparente (PNG) se ve sobre blanco, como en la app.
            var a = c.Alpha / 255f;
            tensor[0, 0, y, x] = c.Blue * a + 255 * (1 - a);
            tensor[0, 1, y, x] = c.Green * a + 255 * (1 - a);
            tensor[0, 2, y, x] = c.Red * a + 255 * (1 - a);
        }
        return (tensor, ancho, alto);
    }

    private static float Solape(Candidato a, Candidato b)
    {
        var ancho = Math.Max(0, Math.Min(a.X2, b.X2) - Math.Max(a.X1, b.X1));
        var alto = Math.Max(0, Math.Min(a.Y2, b.Y2) - Math.Max(a.Y1, b.Y1));
        var interseccion = ancho * alto;
        var union = (a.X2 - a.X1) * (a.Y2 - a.Y1) + (b.X2 - b.X1) * (b.Y2 - b.Y1) - interseccion;
        return union <= 0 ? 0 : interseccion / union;
    }

    public void Dispose()
    {
        if (_sesion.IsValueCreated) _sesion.Value?.Dispose();
    }
}

/// <summary>Revisa una foto de perfil YA publicada en el almacenamiento.</summary>
public interface IRevisorFotoPerfil
{
    /// <summary>
    /// Aprobada, EnRevision o SinRevisar (no se pudo leer o el detector no está disponible); null si la foto no tiene rostro.
    /// </summary>
    Task<EstadoFotoPerfil?> RevisarAsync(string urlFoto, CancellationToken ct = default);
}

public sealed class RevisorFotoPerfil : IRevisorFotoPerfil
{
    private readonly IAlmacenArchivos _almacen;
    private readonly IDetectorRostros _detector;

    public RevisorFotoPerfil(IAlmacenArchivos almacen, IDetectorRostros detector)
    {
        _almacen = almacen; _detector = detector;
    }

    public async Task<EstadoFotoPerfil?> RevisarAsync(string urlFoto, CancellationToken ct = default)
    {
        if (!_almacen.Habilitado || await _almacen.LeerFotoPublicadaAsync(urlFoto, ct) is not { } bytes) return EstadoFotoPerfil.SinRevisar;
        // La inferencia es CPU pura y corta: se saca del hilo de la petición.
        var rostros = await Task.Run(() => _detector.Detectar(bytes), ct);
        return rostros is null ? EstadoFotoPerfil.SinRevisar : ReglaFotoPerfil.Clasificar(rostros);
    }
}
