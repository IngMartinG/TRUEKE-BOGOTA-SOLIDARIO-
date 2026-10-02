using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>Reemplaza la cola de correo: guarda los mensajes para que las pruebas lean los enlaces.</summary>
public sealed partial class CorreoCapturado : ICorreoSaliente
{
    private readonly ConcurrentQueue<MensajeCorreo> _mensajes = new();

    public void Encolar(MensajeCorreo mensaje) => _mensajes.Enqueue(mensaje);

    public IReadOnlyList<MensajeCorreo> Para(string correo)
        => _mensajes.Where(m => string.Equals(m.Para, correo, StringComparison.OrdinalIgnoreCase)).ToList();

    [GeneratedRegex(@"token=([A-Za-z0-9_-]+)")]
    private static partial Regex Token();

    /// <summary>Token del último correo enviado a <paramref name="correo"/> cuyo enlace contiene <paramref name="ruta"/>.</summary>
    public string? UltimoToken(string correo, string ruta)
    {
        var m = Para(correo).LastOrDefault(x => x.Texto.Contains("/" + ruta + "?", StringComparison.Ordinal));
        return m is null ? null : Token().Match(m.Texto).Groups[1].Value;
    }
}
