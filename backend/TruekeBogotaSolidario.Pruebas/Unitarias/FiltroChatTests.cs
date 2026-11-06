using TruekeBogotaSolidario.Negocio.Moderacion;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El filtro del chat bloquea lo ilícito, marca el contacto por fuera y deja pasar la charla normal.</summary>
public class FiltroChatTests
{
    private static readonly ModeracionOpciones Opciones = new() { ArticulosRestringidos = ["boleta falsa"] };

    [Theory]
    [InlineData("Te vendo un arma de fuego, ¿te sirve?")]
    [InlineData("tengo COCAÍNA buena")]
    [InlineData("¿Tienes rivotril o tramadol?")]
    [InlineData("Municiones calibre 38")]
    public void Lo_ilicito_se_bloquea(string texto)
        => Assert.Equal(DecisionFiltroChat.Bloquear, FiltroChat.Evaluar(texto, Opciones).Decision);

    [Theory]
    [InlineData("Escríbeme al 310 555 1234", "teléfono")]
    [InlineData("mi número es +57 3105551234", "teléfono")]
    [InlineData("mejor hablemos por whatsapp", "fuera de Trueke")]
    [InlineData("mira las fotos en www.mitienda.com", "enlace")]
    [InlineData("mándame un correo a ana@correo.co", "correo")]
    [InlineData("también tengo unos cachorros", "artículo prohibido")]
    [InlineData("y una boleta falsa del concierto", "artículo prohibido")]
    public void El_contacto_por_fuera_y_lo_dudoso_se_marca_sin_bloquear(string texto, string motivo)
    {
        var r = FiltroChat.Evaluar(texto, Opciones);
        Assert.Equal(DecisionFiltroChat.Marcar, r.Decision);
        Assert.Contains(motivo, r.Motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("¿Nos vemos el sábado en el parque de Kennedy a las 3?")]
    [InlineData("Te lo dejo en $1.500.000, acepto Nequi")]
    [InlineData("Hay que revolver bien la pintura antes de usarla")]
    [InlineData("Compré unas granadillas en la droguería de la esquina")]
    [InlineData("")]
    public void La_charla_normal_pasa_sin_marca(string texto)
        => Assert.Equal(DecisionFiltroChat.Normal, FiltroChat.Evaluar(texto, Opciones).Decision);
}
