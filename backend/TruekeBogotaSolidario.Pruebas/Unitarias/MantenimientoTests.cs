using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class MantenimientoTests
{
    [Fact]
    public async Task Purga_lo_vencido_y_conserva_lo_vigente()
    {
        using var e = new EntornoNegocio();
        var u = (await e.RegistrarAsync("limpieza")).Usuario.Id;   // deja un refresco vigente y un enlace de verificación usado
        var hace = DateTime.UtcNow.AddDays(-100);

        await e.EnScopeAsync<TruekeDbContext>(async db =>
        {
            db.SesionesRefresh.Add(new SesionRefresh(u, Guid.NewGuid(), new string('a', 64), hace, hace.AddDays(1), hace.AddDays(30)));
            db.TokensUsoUnico.Add(new TokenUsoUnico(u, PropositoToken.RestablecerClave, new string('b', 64), hace, TimeSpan.FromMinutes(30)));
            var leida = new Notificacion(u, "X", "vieja", null, hace);
            leida.MarcarLeida(hace);
            db.Notificaciones.Add(leida);
            db.Notificaciones.Add(new Notificacion(u, "X", "vieja sin leer", null, hace)); // sin leer: se conserva
            await db.SaveChangesAsync();
        });

        var servicio = new MantenimientoHostedService(e.Proveedor.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System,
            NullLogger<MantenimientoHostedService>.Instance);
        Assert.Equal(3, await servicio.LimpiarAsync());

        await e.EnScopeAsync<TruekeDbContext>(async db =>
        {
            Assert.Equal(1, db.SesionesRefresh.Count());                                   // la vigente del registro
            Assert.Equal(1, db.Notificaciones.Count(n => n.Mensaje == "vieja sin leer"));
            Assert.Empty(db.Notificaciones.Where(n => n.Mensaje == "vieja"));
            await Task.CompletedTask;
        });
    }
}
