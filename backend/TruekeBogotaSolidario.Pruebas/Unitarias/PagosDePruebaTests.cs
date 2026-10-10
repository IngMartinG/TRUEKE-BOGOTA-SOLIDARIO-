using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El front avisa "pagos de prueba" solo cuando de verdad no se mueve dinero real.</summary>
public class PagosDePruebaTests
{
    private static PagosOpciones Opciones(string proveedor, string baseUrl, string llavePublica) => new()
    {
        Proveedor = proveedor,
        Wompi = new WompiOpciones { BaseUrl = baseUrl, LlavePublica = llavePublica }
    };

    [Theory]
    [InlineData("Simulado", "https://production.wompi.co/v1", "")]                    // pasarela de desarrollo
    [InlineData("Wompi", "https://sandbox.wompi.co/v1", "pub_test_abc")]              // sandbox de Wompi
    [InlineData("Wompi", "https://production.wompi.co/v1", "pub_test_abc")]           // llaves de prueba mal combinadas: igual es prueba
    public void Es_de_prueba_si_no_hay_cobro_real(string proveedor, string baseUrl, string llave)
        => Assert.True(Opciones(proveedor, baseUrl, llave).EsDePrueba);

    [Fact]
    public void Con_produccion_y_llaves_de_produccion_no_es_de_prueba()
        => Assert.False(Opciones("Wompi", "https://production.wompi.co/v1", "pub_prod_abc").EsDePrueba);
}
