using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class MayoriaEdadUnitariasTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);

    [Theory]
    [InlineData("2008-10-18", 18)] // cumple 18 hoy
    [InlineData("2008-10-19", 17)] // cumple 18 mañana
    [InlineData("1990-01-01", 36)]
    public void Edad_cuenta_si_ya_cumplio_anios(string nacimiento, int esperada)
        => Assert.Equal(esperada, Usuario.EdadEn(DateOnly.Parse(nacimiento), Hoy));

    [Fact]
    public void Quien_nacio_un_29_de_febrero_cumple_el_28_en_anios_no_bisiestos()
    {
        var nacimiento = new DateOnly(2008, 2, 29);
        Assert.Equal(17, Usuario.EdadEn(nacimiento, new DateOnly(2026, 2, 27)));
        Assert.Equal(18, Usuario.EdadEn(nacimiento, new DateOnly(2026, 2, 28)));
    }

    [Theory]
    [InlineData("2008-10-19")] // menor de edad por un día
    [InlineData("2026-10-19")] // futura
    [InlineData("1900-01-01")] // imposible
    public void Fechas_no_validas_se_rechazan(string nacimiento)
        => Assert.Throws<ReglaDeNegocioException>(() => Usuario.ValidarFechaNacimiento(DateOnly.Parse(nacimiento), Hoy));

    [Fact]
    public void Se_registra_una_sola_vez()
    {
        var u = new Usuario("Ana Pérez", "Chapinero", $"ana{Guid.NewGuid():N}@trueke.test", "Clave12345");
        Assert.False(u.MayoriaEdadConfirmada);
        u.RegistrarFechaNacimiento(new DateOnly(1990, 5, 20), Hoy);
        Assert.True(u.MayoriaEdadConfirmada);
        Assert.Throws<ReglaDeNegocioException>(() => u.RegistrarFechaNacimiento(new DateOnly(1980, 1, 1), Hoy));
        Assert.Equal(new DateOnly(1990, 5, 20), u.FechaNacimiento);
    }

    [Fact]
    public void Hoy_en_Colombia_es_UTC_menos_5()
        => Assert.Equal(new DateOnly(2026, 10, 17), Usuario.HoyEnColombia(new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc)));
}
