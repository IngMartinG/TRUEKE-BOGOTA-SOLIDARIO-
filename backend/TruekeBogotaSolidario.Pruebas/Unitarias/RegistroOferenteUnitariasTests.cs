using System.Text.RegularExpressions;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class RegistroOferenteUnitariasTests
{
    [Theory]
    [InlineData("3001234567", "3001234567")]
    [InlineData("300 123 4567", "3001234567")]
    [InlineData("+57 300-123-4567", "3001234567")]
    [InlineData("601 234 5678", "6012345678")]
    public void Acepta_celulares_y_fijos_de_Colombia_y_guarda_solo_los_10_digitos(string telefono, string guardado)
    {
        Assert.Matches(FormatosTexto.Telefono, telefono);
        Assert.Equal(guardado, OferenteService.NormalizarTelefono(telefono));
    }

    [Theory]
    [InlineData("12345")]        // corto
    [InlineData("2001234567")]   // no es celular ni fijo
    [InlineData("609 234 5678")] // indicativo inexistente
    [InlineData("300 123 4567 ext 2")]
    public void Rechaza_lo_que_no_es_un_telefono(string telefono)
        => Assert.DoesNotMatch(new Regex(FormatosTexto.Telefono), telefono);

    [Fact]
    public void El_registro_exige_todos_los_datos_conserva_la_primera_fecha_y_se_borra_con_la_cuenta()
    {
        var u = new Usuario("Ana Pérez", "Chapinero", $"ana{Guid.NewGuid():N}@trueke.test", "Clave12345");
        Assert.False(u.RegistroOferenteCompleto);
        Assert.Throws<ReglaDeNegocioException>(() => u.RegistrarOferente(TipoDocumentoFiscal.CC, "Ana Pérez", "x", "", "11001", "z", DateTime.UtcNow));

        var primera = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        u.RegistrarOferente(TipoDocumentoFiscal.CC, "Ana   Pérez", "doc", "dir", "11001", "tel", primera);
        u.RegistrarOferente(TipoDocumentoFiscal.CE, "Ana Pérez", "doc2", "dir2", "05001", "tel2", primera.AddDays(3));
        Assert.True(u.RegistroOferenteCompleto);
        Assert.Equal("Ana Pérez", u.OferenteNombre);
        Assert.Equal(primera, u.OferenteRegistradoUtc);

        u.Anonimizar(DateTime.UtcNow);
        Assert.False(u.RegistroOferenteCompleto);
        Assert.Null(u.OferenteDocumentoCifrado);
    }
}
