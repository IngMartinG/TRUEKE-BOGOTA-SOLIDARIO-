using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using TruekeBogotaSolidario.Datos.Contexto;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class MigracionesTests
{
    /// <summary>
    /// Si alguien cambia entidades o el DbContext sin generar migración, producción (Database:Inicializacion=Migrate)
    /// arrancaría con un esquema desactualizado. Esta prueba lo detecta en CI. No abre conexión a SQL Server.
    /// </summary>
    [Fact]
    public void El_modelo_no_tiene_cambios_sin_migrar()
    {
        using var db = new TruekeDbContextFactory().CreateDbContext(Array.Empty<string>());
        var migraciones = db.GetService<IMigrationsAssembly>();
        Assert.NotNull(migraciones.ModelSnapshot);
        Assert.Contains(migraciones.Migrations.Keys, k => k.EndsWith("_Inicial", StringComparison.Ordinal));

#pragma warning disable EF1001 // API interna de EF Core: es la misma comprobación que hace "dotnet ef migrations add"
        IModel snapshot = migraciones.ModelSnapshot!.Model;
        if (snapshot is IMutableModel mutable) snapshot = mutable.FinalizeModel();
        snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true, validationLogger: null);

        var diferencias = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshot.GetRelationalModel(), db.GetService<IDesignTimeModel>().Model.GetRelationalModel());
#pragma warning restore EF1001

        Assert.True(diferencias.Count == 0,
            "Hay cambios de modelo sin migración: " + string.Join(", ", diferencias.Select(d => d.GetType().Name)));
    }
}
