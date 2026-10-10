using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class TextoBusquedaTests
{
    [Fact]
    public void Normaliza_sin_tildes_ni_mayusculas_ni_signos()
    {
        Assert.Equal(" camara canon eos 90d ", TextoBusqueda.Normalizar("¡Cámara  CANON (EOS-90D)!"));
        Assert.Equal(" nino pinguino ", TextoBusqueda.Normalizar("Niño", "pingüino"));
        Assert.Equal(" ", TextoBusqueda.Normalizar(null, ""));
    }

    [Theory]
    [InlineData("zapatos", "zapato")]
    [InlineData("pantalones", "pantalon")]
    [InlineData("lápices", "lapiz")]
    [InlineData("relojes", "reloj")]
    [InlineData("televisores", "televisor")]
    [InlineData("clases", "clase")]
    [InlineData("mesa", "mesa")]
    [InlineData("gas", "gas")]
    [InlineData("ps5", "ps5")]
    public void Lleva_los_plurales_simples_al_singular(string palabra, string esperado)
        => Assert.Equal(esperado, TextoBusqueda.Singular(TextoBusqueda.Normalizar(palabra).Trim()));

    [Fact]
    public void Los_terminos_quitan_palabras_vacias_y_repetidas_y_tienen_limite()
    {
        Assert.Equal(new[] { "bicicleta", "roja", "nino" }, TextoBusqueda.Terminos("Bicicletas rojas para el NIÑO, bicicleta"));
        Assert.Empty(TextoBusqueda.Terminos("de la para"));
        Assert.Empty(TextoBusqueda.Terminos("   "));
        Assert.Equal(TextoBusqueda.MaxTerminos, TextoBusqueda.Terminos("uno dos tres cuatro cinco seis siete ocho").Count);
        // nada que el LIKE de SQL Server interprete como comodín
        Assert.All(TextoBusqueda.Terminos("50% [oferta] mesa_auxiliar"), t => Assert.DoesNotContain(t, c => c is '%' or '_' or '['));
    }
}
