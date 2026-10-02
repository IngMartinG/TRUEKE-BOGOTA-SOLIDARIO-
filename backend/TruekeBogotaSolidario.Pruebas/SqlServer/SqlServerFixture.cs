using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace TruekeBogotaSolidario.Pruebas.SqlServer;

/// <summary>
/// SQL Server 2022 REAL en un contenedor (Testcontainers). Si la máquina no tiene Docker, las pruebas se marcan como
/// OMITIDAS (no como aprobadas). En GitHub Actions (ubuntu-latest trae Docker) se ejecutan siempre.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private MsSqlContainer? _contenedor;

    public bool Disponible { get; private set; }
    public string MotivoNoDisponible { get; private set; } = "";

    public async Task InitializeAsync()
    {
        try
        {
            _contenedor = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _contenedor.StartAsync();
            Disponible = true;
        }
        catch (Exception ex)
        {
            Disponible = false;
            MotivoNoDisponible = $"SQL Server en Docker no disponible ({ex.GetType().Name}). Estas pruebas corren en el CI.";
        }
    }

    /// <summary>Cadena hacia una base NUEVA (cada prueba la crea con Migrate): pruebas aisladas entre sí.</summary>
    public string NuevaBase()
        => new SqlConnectionStringBuilder(_contenedor!.GetConnectionString()) { InitialCatalog = "trueke_" + Guid.NewGuid().ToString("N")[..12] }.ConnectionString;

    public async Task DisposeAsync()
    {
        if (_contenedor is not null) await _contenedor.DisposeAsync();
    }
}
