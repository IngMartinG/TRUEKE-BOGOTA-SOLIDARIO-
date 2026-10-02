using System.Text.Json;
using System.Text.Json.Nodes;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

/// <summary>
/// docs/openapi.json es el contrato que consume Angular (p. ej. con openapi-generator / ng-openapi-gen).
/// Esta prueba falla si la API cambió y el archivo no se regeneró. Para regenerarlo:
///   ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests
/// </summary>
public class OpenApiTests
{
    private static string RutaContrato()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("No se encontró la raíz del repositorio (carpeta que contiene backend/).");
        return Path.Combine(dir.FullName, "docs", "openapi.json");
    }

    private static string Normalizar(string json)
        => JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";

    [Fact]
    public async Task docs_openapi_json_esta_actualizado()
    {
        await using var fabrica = new FabricaApiDesarrollo();
        var actual = Normalizar(await fabrica.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));
        var ruta = RutaContrato();

        if (Environment.GetEnvironmentVariable("ACTUALIZAR_OPENAPI") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            await File.WriteAllTextAsync(ruta, actual, new System.Text.UTF8Encoding(false));
            return;
        }

        Assert.True(File.Exists(ruta), $"No existe {ruta}. Genéralo con: ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests");
        var guardado = (await File.ReadAllTextAsync(ruta)).ReplaceLineEndings("\n");
        Assert.True(guardado == actual,
            "docs/openapi.json no coincide con la API. Regenéralo con: ACTUALIZAR_OPENAPI=1 dotnet test --filter OpenApiTests");
    }
}
