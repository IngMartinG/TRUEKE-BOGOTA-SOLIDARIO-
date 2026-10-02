using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TruekeBogotaSolidario.Negocio.Pagos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class WebhookWompiTests
{
    private const string Secreto = "secreto-eventos";

    /// <summary>Construye un evento como los de Wompi: checksum = SHA256(valores de properties + timestamp + secreto).</summary>
    public static string EventoFirmado(string secreto, string transaccionId = "1234-abc", string estado = "APPROVED",
        long montoCentavos = 1_000_000, string referencia = "TRK-1", long timestamp = 1_700_000_000, string? checksumForzado = null)
    {
        var concatenado = $"{transaccionId}{estado}{montoCentavos}{timestamp}{secreto}";
        var checksum = checksumForzado ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(concatenado))).ToLowerInvariant();
        return JsonSerializer.Serialize(new
        {
            @event = "transaction.updated",
            data = new { transaction = new { id = transaccionId, status = estado, amount_in_cents = montoCentavos, reference = referencia, currency = "COP" } },
            signature = new { properties = new[] { "transaction.id", "transaction.status", "transaction.amount_in_cents" }, checksum },
            timestamp,
            sent_at = "2026-10-02T12:00:00.000Z"
        });
    }

    [Fact]
    public void Firma_valida_es_aceptada()
    {
        var e = VerificadorEventosWompi.Verificar(EventoFirmado(Secreto), Secreto);
        Assert.NotNull(e);
        Assert.Equal("transaction.updated", e!.Evento);
        Assert.Equal("1234-abc", e.TransaccionId);
    }

    [Fact]
    public void Firma_con_otro_secreto_es_rechazada()
        => Assert.Null(VerificadorEventosWompi.Verificar(EventoFirmado("otro-secreto"), Secreto));

    [Fact]
    public void Datos_alterados_despues_de_firmar_son_rechazados()
    {
        var original = EventoFirmado(Secreto);
        var alterado = original.Replace("1000000", "99900000");
        Assert.NotEqual(original, alterado);
        Assert.Null(VerificadorEventosWompi.Verificar(alterado, Secreto));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no es json")]
    [InlineData("{}")]
    [InlineData("{\"event\":\"transaction.updated\"}")]
    public void Cuerpos_malformados_son_rechazados_sin_excepcion(string cuerpo)
        => Assert.Null(VerificadorEventosWompi.Verificar(cuerpo, Secreto));

    [Fact]
    public void Sin_secreto_configurado_nada_es_valido()
        => Assert.Null(VerificadorEventosWompi.Verificar(EventoFirmado(""), ""));

    [Fact]
    public void Checksum_vacio_o_truncado_es_rechazado()
    {
        Assert.Null(VerificadorEventosWompi.Verificar(EventoFirmado(Secreto, checksumForzado: ""), Secreto));
        Assert.Null(VerificadorEventosWompi.Verificar(EventoFirmado(Secreto, checksumForzado: "abc"), Secreto));
    }
}
