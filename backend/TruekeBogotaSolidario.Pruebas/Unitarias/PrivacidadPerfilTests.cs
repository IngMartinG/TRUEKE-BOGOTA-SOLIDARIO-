using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class PrivacidadPerfilTests
{
    [Fact]
    public async Task Un_visitante_anonimo_no_ve_si_alguien_esta_en_linea()
    {
        using var e = new EntornoNegocio();
        var ana = (await e.RegistrarAsync("anaEnLinea")).Usuario.Id;
        var beto = (await e.RegistrarAsync("betoMira")).Usuario.Id;
        await e.EnScopeAsync<IPresencia>(p => p.ConectarAsync(ana, "conexion-1"));

        var anonimo = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(null, ana));
        Assert.False(anonimo.Confianza.EnLinea);

        var conSesion = await e.EnScopeAsync<IPublicacionService, PerfilUsuarioDto>(s => s.ObtenerPerfilAsync(beto, ana));
        Assert.True(conSesion.Confianza.EnLinea);
    }

    [Theory]
    [InlineData("ana@trueke.test", "ANA@trueke.test", true)]
    [InlineData("ana.perez+trueke@gmail.com", "anaperez@gmail.com", true)]   // en Gmail es el mismo buzón
    [InlineData("ana@googlemail.com", "ana@gmail.com", true)]
    [InlineData("ana+otra@empresa.test", "ana@empresa.test", false)]         // fuera de Gmail puede ser otro buzón
    [InlineData("a.na@empresa.test", "ana@empresa.test", false)]
    public void Google_solo_se_vincula_con_el_mismo_buzon(string cuenta, string google, bool mismo)
        => Assert.Equal(mismo, AuthService.MismoBuzon(cuenta, google));
}
