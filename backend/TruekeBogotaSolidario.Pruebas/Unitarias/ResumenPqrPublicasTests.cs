using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;
using TruekeBogotaSolidario.Negocio.Servicios;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Las PQRS públicas avisan a la moderación en un solo resumen por ventana; las de usuarios con cuenta, una por una.</summary>
public class ResumenPqrPublicasTests
{
    private static CrearPqrPublicaRequest Publica(int i) => new()
    {
        Nombre = "Ana Pérez", Correo = $"publica{i}{Guid.NewGuid():N}@t.co", Tipo = TipoPqrDto.Peticion,
        Asunto = "No puedo ingresar", Descripcion = "Al ingresar aparece un error y no avanza."
    };

    private static int ContarAvisos(EntornoNegocio e, Guid moderador)
    {
        using var scope = e.Proveedor.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Notificaciones
            .Count(n => n.UsuarioId == moderador && n.Tipo == TiposNotificacion.PqrNueva);
    }

    [Fact]
    public async Task Las_publicas_se_agrupan_en_un_aviso_por_moderador_y_las_de_cuenta_avisan_una_por_una()
    {
        using var e = new EntornoNegocio();
        var moderador = (await e.RegistrarAsync("moderador")).Usuario.Id;
        await e.HacerModeradorAsync(moderador);
        var otro = (await e.RegistrarAsync("moderador2")).Usuario.Id;
        await e.HacerModeradorAsync(otro, Datos.Entidades.RolUsuarioEnum.Administrador);

        for (var i = 0; i < 3; i++)
            await e.EnScopeAsync<IPqrService, PqrDto>(s => s.CrearPublicaAsync(Publica(i)));
        Assert.Equal(0, ContarAvisos(e, moderador)); // ninguna avisa sola

        var servicio = new ResumenPqrPublicasHostedService(e.Proveedor.GetRequiredService<ResumenPqrPublicas>(),
            e.Proveedor.GetRequiredService<IServiceScopeFactory>(), NullLogger<ResumenPqrPublicasHostedService>.Instance);
        Assert.Equal(3, await servicio.EnviarAsync());
        foreach (var id in new[] { moderador, otro })
        {
            using var scope = e.Proveedor.CreateScope();
            var aviso = Assert.Single(scope.ServiceProvider.GetRequiredService<TruekeDbContext>().Notificaciones
                .Where(n => n.UsuarioId == id && n.Tipo == TiposNotificacion.PqrNueva));
            Assert.Equal("3 PQRS públicas nuevas: revísalas en PQRS.", aviso.Mensaje);
            Assert.DoesNotContain("No puedo ingresar", aviso.Mensaje); // el texto de quien radicó no viaja en el aviso
        }

        Assert.Equal(0, await servicio.EnviarAsync()); // la ventana siguiente, sin PQRS nuevas, no avisa
        Assert.Equal(1, ContarAvisos(e, moderador));

        // Con cuenta: aviso inmediato e individual
        var cliente = (await e.RegistrarAsync("cliente")).Usuario.Id;
        await e.EnScopeAsync<IPqrService, PqrDto>(s => s.CrearAsync(cliente, new CrearPqrRequest
        {
            Tipo = TipoPqrDto.Queja, Asunto = "Publicación engañosa", Descripcion = "La publicación no corresponde al objeto."
        }));
        Assert.Equal(2, ContarAvisos(e, moderador));
    }

    [Fact]
    public void El_mensaje_distingue_una_de_varias()
    {
        Assert.Equal("Nueva PQRS pública: revísala en PQRS.", ResumenPqrPublicas.Mensaje(1));
        Assert.StartsWith("12 PQRS públicas nuevas", ResumenPqrPublicas.Mensaje(12));
        var r = new ResumenPqrPublicas();
        r.Registrar(); r.Registrar();
        Assert.Equal(2, r.Extraer());
        r.Devolver(2); r.Registrar();
        Assert.Equal(3, r.Extraer());
        Assert.Equal(0, r.Extraer());
    }
}
