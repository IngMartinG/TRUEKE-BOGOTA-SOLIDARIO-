using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;

namespace TruekeBogotaSolidario.Negocio.Asistente;

/// <summary>Groq: API compatible con OpenAI, con nivel gratis y herramientas (tool calling).</summary>
public sealed class GroqOpciones
{
    /// <summary>console.groq.com → API Keys (gsk_...). Es un secreto: solo por variable de entorno (Asistente__Groq__ClaveApi).</summary>
    public string ClaveApi { get; set; } = "";
    [Required] public string Modelo { get; set; } = "openai/gpt-oss-120b";
    [Required] public string Url { get; set; } = "https://api.groq.com/openai/v1/chat/completions";
    /// <summary>"low" ahorra tokens del cupo gratis en los modelos que razonan (gpt-oss). Vacío = no se envía.</summary>
    public string EsfuerzoRazonamiento { get; set; } = "low";
}

public sealed class AsistenteOpciones
{
    public const string Seccion = "Asistente";
    public const string ProveedorGroq = "Groq";
    /// <summary>"Groq" o "Desactivado" (entonces el front usa solo la ayuda local).</summary>
    public string Proveedor { get; set; } = "Desactivado";
    /// <summary>Preguntas con IA por persona y día: protege el cupo gratis compartido por toda la comunidad.</summary>
    [Range(1, 500)] public int MensajesPorDia { get; set; } = 20;
    /// <summary>Turnos anteriores de la conversación que se envían como contexto (el historial no se guarda en el servidor).</summary>
    [Range(0, 10)] public int MaxTurnos { get; set; } = 6;
    public GroqOpciones Groq { get; set; } = new();
    public bool EsGroq => string.Equals(Proveedor, ProveedorGroq, StringComparison.OrdinalIgnoreCase);
}

public enum RolMensaje { Sistema, Usuario, Asistente, Herramienta }

public sealed record LlamadaHerramienta(string Id, string Nombre, string ArgumentosJson);

/// <param name="Llamadas">Herramientas que pidió el modelo (solo en mensajes del asistente).</param>
/// <param name="IdLlamada">A qué llamada responde (solo en mensajes de herramienta).</param>
public sealed record MensajeModelo(RolMensaje Rol, string Contenido, IReadOnlyList<LlamadaHerramienta>? Llamadas = null, string? IdLlamada = null);

/// <param name="Parametros">Esquema JSON de los argumentos.</param>
public sealed record HerramientaModelo(string Nombre, string Descripcion, JsonObject Parametros);

/// <summary>Un modelo de lenguaje con herramientas. Cualquier falla (cupo, red, tiempo) se reporta como <see cref="ModeloNoDisponibleException"/>.</summary>
public interface IModeloLenguaje
{
    bool Habilitado { get; }
    Task<MensajeModelo> CompletarAsync(IReadOnlyList<MensajeModelo> mensajes, IReadOnlyList<HerramientaModelo> herramientas, CancellationToken ct);
}

public sealed class ModeloNoDisponibleException : Exception
{
    public ModeloNoDisponibleException(string mensaje, Exception? causa = null) : base(mensaje, causa) { }
}

public sealed class ModeloDesactivado : IModeloLenguaje
{
    public bool Habilitado => false;
    public Task<MensajeModelo> CompletarAsync(IReadOnlyList<MensajeModelo> mensajes, IReadOnlyList<HerramientaModelo> herramientas, CancellationToken ct)
        => throw new ModeloNoDisponibleException("Asistente:Proveedor=Desactivado.");
}

/// <summary>
/// Groq por su API HTTPS compatible con OpenAI. Sin seguir redirecciones: la llave solo viaja al host configurado. Un 429
/// (cupo gratis agotado), un error del servidor o la demora se informan sin el cuerpo de la respuesta (puede repetir la pregunta).
/// </summary>
public sealed class ModeloGroq : IModeloLenguaje
{
    public const string ClienteHttp = "asistente-groq";
    private const int MaxTokensRespuesta = 700;

    private readonly IHttpClientFactory _http;
    private readonly GroqOpciones _o;

    public ModeloGroq(IHttpClientFactory http, IOptions<AsistenteOpciones> o)
    {
        _http = http; _o = o.Value.Groq;
    }

    public bool Habilitado => true;

    public async Task<MensajeModelo> CompletarAsync(IReadOnlyList<MensajeModelo> mensajes, IReadOnlyList<HerramientaModelo> herramientas, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, _o.Url)
        {
            Content = new StringContent(CuerpoPeticion(mensajes, herramientas).ToJsonString(), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _o.ClaveApi);
        req.Headers.Accept.ParseAdd("application/json");

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await _http.CreateClient(ClienteHttp).SendAsync(req, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new ModeloNoDisponibleException("Groq no respondió.", ex);
        }
        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
                throw new ModeloNoDisponibleException($"Groq rechazó la petición (HTTP {(int)respuesta.StatusCode}).");
            try
            {
                using var doc = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                return LeerRespuesta(doc.RootElement);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
            {
                throw new ModeloNoDisponibleException("Groq devolvió una respuesta inesperada.", ex);
            }
        }
    }

    internal JsonObject CuerpoPeticion(IReadOnlyList<MensajeModelo> mensajes, IReadOnlyList<HerramientaModelo> herramientas)
    {
        var cuerpo = new JsonObject
        {
            ["model"] = _o.Modelo,
            ["messages"] = new JsonArray(mensajes.Select(MensajeJson).ToArray<JsonNode?>()),
            ["temperature"] = 0.3,
            ["max_completion_tokens"] = MaxTokensRespuesta,
        };
        if (!string.IsNullOrWhiteSpace(_o.EsfuerzoRazonamiento)) cuerpo["reasoning_effort"] = _o.EsfuerzoRazonamiento;
        if (herramientas.Count > 0)
        {
            cuerpo["tools"] = new JsonArray(herramientas.Select(h => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = h.Nombre,
                    ["description"] = h.Descripcion,
                    ["parameters"] = h.Parametros.DeepClone(),
                },
            }).ToArray());
            cuerpo["tool_choice"] = "auto";
        }
        return cuerpo;
    }

    private static JsonObject MensajeJson(MensajeModelo m)
    {
        var o = new JsonObject
        {
            ["role"] = m.Rol switch
            {
                RolMensaje.Sistema => "system",
                RolMensaje.Usuario => "user",
                RolMensaje.Asistente => "assistant",
                _ => "tool",
            },
            ["content"] = m.Contenido,
        };
        if (m.Llamadas is { Count: > 0 })
            o["tool_calls"] = new JsonArray(m.Llamadas.Select(l => (JsonNode?)new JsonObject
            {
                ["id"] = l.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = l.Nombre, ["arguments"] = l.ArgumentosJson },
            }).ToArray());
        if (m.IdLlamada is not null) o["tool_call_id"] = m.IdLlamada;
        return o;
    }

    internal static MensajeModelo LeerRespuesta(JsonElement raiz)
    {
        var mensaje = raiz.GetProperty("choices")[0].GetProperty("message");
        var contenido = mensaje.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : "";
        List<LlamadaHerramienta>? llamadas = null;
        if (mensaje.TryGetProperty("tool_calls", out var tc) && tc.ValueKind == JsonValueKind.Array)
            llamadas = tc.EnumerateArray().Select(l => new LlamadaHerramienta(
                l.GetProperty("id").GetString()!,
                l.GetProperty("function").GetProperty("name").GetString()!,
                l.GetProperty("function").TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString()! : "{}")).ToList();
        return new MensajeModelo(RolMensaje.Asistente, contenido, llamadas);
    }
}
