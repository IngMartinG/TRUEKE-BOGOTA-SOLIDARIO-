using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class TotpTests
{
    private static readonly byte[] SecretoRfc = Encoding.ASCII.GetBytes("12345678901234567890");

    /// <summary>Vectores del RFC 6238 (SHA1), tomando los 6 últimos dígitos.</summary>
    [Theory]
    [InlineData(59, "287082")]
    [InlineData(1111111109, "081804")]
    [InlineData(1234567890, "005924")]
    [InlineData(2000000000, "279037")]
    public void Coincide_con_los_vectores_del_RFC_6238(long unix, string esperado)
        => Assert.Equal(esperado, Totp.Calcular(SecretoRfc, Totp.Paso(DateTimeOffset.FromUnixTimeSeconds(unix))));

    [Fact]
    public void Base32_estandar() => Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", Totp.Base32(SecretoRfc));

    [Fact]
    public void Acepta_el_paso_vecino_y_rechaza_codigos_viejos_o_malformados()
    {
        var ahora = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var paso = Totp.Paso(ahora);
        Assert.Equal(paso - 1, Totp.Verificar(SecretoRfc, Totp.Calcular(SecretoRfc, paso - 1), ahora));
        Assert.Null(Totp.Verificar(SecretoRfc, Totp.Calcular(SecretoRfc, paso - 3), ahora));
        Assert.Null(Totp.Verificar(SecretoRfc, "12345", ahora));
        Assert.Null(Totp.Verificar(SecretoRfc, "abcdef", ahora));
        Assert.Equal(paso, Totp.Verificar(SecretoRfc, Totp.Calcular(SecretoRfc, paso).Insert(3, " "), ahora)); // "123 456"
    }

    [Fact]
    public void Codigos_de_recuperacion_unicos_y_hash_insensible_a_formato()
    {
        var codigos = Totp.GenerarCodigosRecuperacion();
        Assert.Equal(10, codigos.Distinct().Count());
        Assert.All(codigos, c => Assert.Matches("^[A-Z2-9]{4}-[A-Z2-9]{4}$", c));
        Assert.Equal(Totp.HashCodigoRecuperacion(codigos[0]), Totp.HashCodigoRecuperacion(" " + codigos[0].ToLowerInvariant() + " "));
    }

    [Fact]
    public void Cifrado_AES_GCM_ida_y_vuelta_y_detecta_alteraciones()
    {
        var cifrador = new CifradorAesGcm(Options.Create(new SeguridadOpciones { ClaveCifrado = FabricaApi.ClaveCifradoPruebas }));
        var cifrado = cifrador.Cifrar(SecretoRfc);
        Assert.Equal(SecretoRfc, cifrador.Descifrar(cifrado));
        Assert.NotEqual(cifrado, cifrador.Cifrar(SecretoRfc)); // nonce aleatorio

        var bytes = Convert.FromBase64String(cifrado);
        bytes[^1] ^= 0xFF;
        Assert.ThrowsAny<CryptographicException>(() => cifrador.Descifrar(Convert.ToBase64String(bytes)));
        Assert.Throws<InvalidOperationException>(() => new CifradorAesGcm(Options.Create(new SeguridadOpciones { ClaveCifrado = "corta" })));
    }
}
