using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class PlantillaCorreoTests
{
    [Fact]
    public void Genera_html_y_texto_y_escapa_el_contenido_variable()
    {
        var m = PlantillaCorreo.Crear("a@b.co", "Asunto", "Hola <script>alert(1)</script>:",
            new[] { "Línea con \"comillas\" & <b>etiquetas</b>" }, ("Abrir", "https://app.trueke.co/x?token=abc&y=\"z\""), "Pie");

        Assert.NotNull(m.Html);
        Assert.DoesNotContain("<script>", m.Html);
        Assert.Contains("&lt;script&gt;", m.Html);
        Assert.Contains("&lt;b&gt;etiquetas&lt;/b&gt;", m.Html);
        Assert.Contains("href=\"https://app.trueke.co/x?token=abc&amp;y=&quot;z&quot;\"", m.Html);
        Assert.Contains("Abrir: https://app.trueke.co/x?token=abc&y=\"z\"", m.Texto);   // texto plano con el enlace completo
        Assert.StartsWith("Hola <script>", m.Texto);
    }
}
