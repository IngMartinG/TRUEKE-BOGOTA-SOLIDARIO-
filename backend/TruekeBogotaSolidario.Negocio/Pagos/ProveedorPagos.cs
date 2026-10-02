using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Pagos;

/// <summary>Estado de una transacción según la pasarela (fuente de verdad: se consulta a la pasarela, no se confía en el cliente).</summary>
public sealed record TransaccionProveedor(string Id, string Referencia, long MontoCentavos, string Moneda, string Estado);

public interface IProveedorPagos
{
    string Nombre { get; }
    string? LlavePublica { get; }
    string? FirmaIntegridad(string referencia, long montoCentavos, string moneda);
    Task<TransaccionProveedor?> ConsultarPorIdAsync(string transaccionId, CancellationToken ct);
    Task<TransaccionProveedor?> ConsultarPorReferenciaAsync(string referencia, CancellationToken ct);
}

/// <summary>Solo desarrollo/pruebas. El arranque de la API lo rechaza en Producción.</summary>
public sealed class ProveedorPagosSimulado : IProveedorPagos
{
    public string Nombre => "Simulado";
    public string? LlavePublica => null;
    public string? FirmaIntegridad(string referencia, long montoCentavos, string moneda) => null;
    public Task<TransaccionProveedor?> ConsultarPorIdAsync(string transaccionId, CancellationToken ct) => Task.FromResult<TransaccionProveedor?>(null);
    public Task<TransaccionProveedor?> ConsultarPorReferenciaAsync(string referencia, CancellationToken ct) => Task.FromResult<TransaccionProveedor?>(null);
}

public sealed class ProveedorPagosWompi : IProveedorPagos
{
    private readonly HttpClient _http;
    private readonly WompiOpciones _o;
    private readonly ILogger<ProveedorPagosWompi> _log;

    public ProveedorPagosWompi(HttpClient http, IOptions<PagosOpciones> opciones, ILogger<ProveedorPagosWompi> log)
    {
        _http = http;
        _o = opciones.Value.Wompi;
        _log = log;
    }

    public string Nombre => "Wompi";
    public string? LlavePublica => _o.LlavePublica;

    /// <summary>SHA256(referencia + montoEnCentavos + moneda + secretoIntegridad), hex minúscula (firma de integridad del widget de Wompi).</summary>
    public string? FirmaIntegridad(string referencia, long montoCentavos, string moneda)
    {
        var cadena = referencia + montoCentavos.ToString(CultureInfo.InvariantCulture) + moneda + _o.SecretoIntegridad;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cadena))).ToLowerInvariant();
    }

    public async Task<TransaccionProveedor?> ConsultarPorIdAsync(string transaccionId, CancellationToken ct)
    {
        if (transaccionId.Length is 0 or > 100 || !transaccionId.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            return null; // evita inyectar rutas en la URL de la pasarela
        using var req = Crear(HttpMethod.Get, $"{_o.BaseUrl.TrimEnd('/')}/transactions/{Uri.EscapeDataString(transaccionId)}");
        using var resp = await _http.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement.TryGetProperty("data", out var d) ? Leer(d) : null;
    }

    public async Task<TransaccionProveedor?> ConsultarPorReferenciaAsync(string referencia, CancellationToken ct)
    {
        using var req = Crear(HttpMethod.Get, $"{_o.BaseUrl.TrimEnd('/')}/transactions?reference={Uri.EscapeDataString(referencia)}");
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return null;
        var todas = data.EnumerateArray().Select(Leer).Where(t => t is not null).Select(t => t!).ToList();
        // Si hubo varios intentos con la misma referencia, el aprobado gana
        return todas.FirstOrDefault(t => t.Estado == "APPROVED") ?? todas.FirstOrDefault();
    }

    private HttpRequestMessage Crear(HttpMethod metodo, string url)
    {
        var req = new HttpRequestMessage(metodo, url);
        if (!string.IsNullOrWhiteSpace(_o.LlavePrivada))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.LlavePrivada);
        return req;
    }

    private TransaccionProveedor? Leer(JsonElement d)
    {
        try
        {
            return new TransaccionProveedor(
                d.GetProperty("id").GetString() ?? "",
                d.GetProperty("reference").GetString() ?? "",
                d.GetProperty("amount_in_cents").GetInt64(),
                d.GetProperty("currency").GetString() ?? "",
                (d.GetProperty("status").GetString() ?? "").ToUpperInvariant());
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            _log.LogWarning("Respuesta de la pasarela con formato inesperado");
            return null;
        }
    }
}

/// <summary>Verifica la firma de los eventos (webhooks) de Wompi: SHA256(valores de signature.properties + timestamp + secretoEventos).</summary>
public static class VerificadorEventosWompi
{
    public sealed record EventoVerificado(string Evento, string TransaccionId);

    public static EventoVerificado? Verificar(string cuerpoJson, string secretoEventos)
    {
        if (string.IsNullOrWhiteSpace(secretoEventos) || string.IsNullOrWhiteSpace(cuerpoJson) || cuerpoJson.Length > 100_000) return null;
        try
        {
            using var doc = JsonDocument.Parse(cuerpoJson);
            var raiz = doc.RootElement;
            var evento = raiz.GetProperty("event").GetString() ?? "";
            var data = raiz.GetProperty("data");
            var firma = raiz.GetProperty("signature");
            var checksum = firma.GetProperty("checksum").GetString() ?? "";
            var timestamp = raiz.GetProperty("timestamp").GetRawText().Trim('"');

            var sb = new StringBuilder();
            foreach (var prop in firma.GetProperty("properties").EnumerateArray())
            {
                JsonElement actual = data;
                foreach (var parte in (prop.GetString() ?? "").Split('.'))
                    actual = actual.GetProperty(parte);
                sb.Append(actual.ValueKind == JsonValueKind.String ? actual.GetString() : actual.GetRawText());
            }
            sb.Append(timestamp).Append(secretoEventos);

            var calculado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
            var esperado = checksum.ToLowerInvariant();
            if (calculado.Length != esperado.Length ||
                !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(calculado), Encoding.ASCII.GetBytes(esperado)))
                return null;

            var id = data.GetProperty("transaction").GetProperty("id").GetString() ?? "";
            return id.Length == 0 ? null : new EventoVerificado(evento, id);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
