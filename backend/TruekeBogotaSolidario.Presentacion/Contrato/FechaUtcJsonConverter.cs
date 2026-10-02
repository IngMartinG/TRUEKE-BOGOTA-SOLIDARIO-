using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TruekeBogotaSolidario.Presentacion.Contrato;

/// <summary>
/// Todas las fechas viajan en UTC ISO-8601 con "Z" (p. ej. 2026-10-02T15:04:05.123Z).
/// SQL Server devuelve DateTime con Kind=Unspecified: sin este conversor saldrían sin "Z" y el navegador
/// las interpretaría como hora local (en Bogotá, 5 horas de error). Al leer, cualquier fecha se normaliza a UTC.
/// </summary>
public sealed class FechaUtcJsonConverter : JsonConverter<DateTime>
{
    private const string Formato = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        if (!DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var valor))
            throw new JsonException("Fecha no válida. Usa ISO-8601 (por ejemplo 2026-10-02T15:04:05Z).");
        return valor.UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc) // la aplicación siempre guarda UTC
        };
        writer.WriteStringValue(utc.ToString(Formato, CultureInfo.InvariantCulture));
    }
}
