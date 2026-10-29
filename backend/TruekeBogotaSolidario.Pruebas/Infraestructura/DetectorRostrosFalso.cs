using TruekeBogotaSolidario.Negocio.Archivos;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>Detector controlable: devuelve los rostros que la prueba indique (por defecto, una cara clara que llena la foto).</summary>
public sealed class DetectorRostrosFalso : IDetectorRostros
{
    public static readonly IReadOnlyList<RostroDetectado> CaraClara = [new(0.99f, 0.3f)];
    public static readonly IReadOnlyList<RostroDetectado> SinRostro = [];
    public static readonly IReadOnlyList<RostroDetectado> Dudosa = [new(0.8f, 0.3f)];

    public IReadOnlyList<RostroDetectado>? Resultado { get; set; } = CaraClara;
    /// <summary>Si se indica, decide según la imagen (tiene prioridad sobre <see cref="Resultado"/>).</summary>
    public Func<byte[], IReadOnlyList<RostroDetectado>?>? PorImagen { get; set; }

    public IReadOnlyList<RostroDetectado>? Detectar(byte[] imagen) => PorImagen is { } decidir ? decidir(imagen) : Resultado;
}
