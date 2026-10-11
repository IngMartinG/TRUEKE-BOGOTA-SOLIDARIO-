using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace TruekeBogotaSolidario.Pruebas.Azurite;

/// <summary>
/// Azurite (emulador oficial de Azure Blob) en un contenedor, para probar el almacén REAL con SAS de verdad.
/// Si la máquina no tiene Docker, las pruebas se marcan como OMITIDAS (no como aprobadas). En el CI corren siempre.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    /// <summary>Llave pública y conocida del emulador (la misma en todas las instalaciones de Azurite): no es un secreto.</summary>
    private const string LlaveEmulador = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    private IContainer? _contenedor;

    public bool Disponible { get; private set; }
    public string MotivoNoDisponible { get; private set; } = "";
    public string CadenaConexion { get; private set; } = "";

    public async Task InitializeAsync()
    {
        try
        {
            _contenedor = new ContainerBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
                .WithCommand("azurite-blob", "--blobHost", "0.0.0.0", "--blobPort", "10000", "--inMemoryPersistence", "--skipApiVersionCheck")
                .WithPortBinding(10000, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("successfully listen"))
                .Build();
            await _contenedor.StartAsync();
            CadenaConexion = $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={LlaveEmulador};" +
                             $"BlobEndpoint=http://{_contenedor.Hostname}:{_contenedor.GetMappedPublicPort(10000)}/devstoreaccount1;";
            Disponible = true;
        }
        catch (Exception ex)
        {
            Disponible = false;
            MotivoNoDisponible = $"Azurite en Docker no disponible ({ex.GetType().Name}). Estas pruebas corren en el CI.";
        }
    }

    public async Task DisposeAsync()
    {
        if (_contenedor is not null) await _contenedor.DisposeAsync();
    }
}
