using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Testcontainers.PostgreSql;

namespace TruekeBogotaSolidario.Pruebas.SupabaseStorage;

/// <summary>
/// El servidor de Storage de Supabase (supabase/storage-api, el mismo que corre en Supabase) en un contenedor, con su
/// PostgreSQL y los archivos en disco, para probar el almacén REAL con URLs firmadas de verdad. Si la máquina no tiene
/// Docker, las pruebas se marcan como OMITIDAS (no como aprobadas). En el CI corren siempre.
/// </summary>
public sealed class StorageFixture : IAsyncLifetime
{
    /// <summary>Imagen fijada por digest (la misma que usan las pruebas de punta a punta).</summary>
    public const string Imagen = "supabase/storage-api:v1.79.28@sha256:1f6c99d3952d78d57129802aa2ae8a80d75e327492cf24742fd0aeaf90967b96";
    /// <summary>Secreto de un solo uso de este contenedor: firma la llave de servicio de las pruebas.</summary>
    private static readonly string SecretoJwt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private INetwork? _red;
    private PostgreSqlContainer? _postgres;
    private IContainer? _storage;

    public bool Disponible { get; private set; }
    public string MotivoNoDisponible { get; private set; } = "";
    public string Url { get; private set; } = "";
    public string LlaveServicio { get; } = Jwt("service_role");
    public string LlaveAnonima { get; } = Jwt("anon");

    public async Task InitializeAsync()
    {
        try
        {
            _red = new NetworkBuilder().Build();
            _postgres = new PostgreSqlBuilder("postgres:17-alpine").WithNetwork(_red).WithNetworkAliases("db")
                .WithDatabase("storage").WithUsername("postgres").WithPassword("postgres").Build();
            await _postgres.StartAsync();
            _storage = new ContainerBuilder(Imagen)
                .WithNetwork(_red)
                .WithEnvironment(new Dictionary<string, string>
                {
                    ["DATABASE_URL"] = "postgres://postgres:postgres@db:5432/storage",
                    ["DB_INSTALL_ROLES"] = "true",
                    ["AUTH_JWT_SECRET"] = SecretoJwt,
                    ["PGRST_JWT_SECRET"] = SecretoJwt,
                    ["ANON_KEY"] = LlaveAnonima,
                    ["SERVICE_KEY"] = LlaveServicio,
                    ["STORAGE_BACKEND"] = "file",
                    ["FILE_STORAGE_BACKEND_PATH"] = "/tmp/storage",
                    ["FILE_SIZE_LIMIT"] = "52428800",
                    ["TENANT_ID"] = "pruebas",
                    ["REGION"] = "local",
                    ["GLOBAL_S3_BUCKET"] = "pruebas"
                })
                .WithPortBinding(5000, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Started Successfully"))
                .Build();
            await _storage.StartAsync();
            Url = $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(5000)}";
            Disponible = true;
        }
        catch (Exception ex)
        {
            Disponible = false;
            MotivoNoDisponible = $"Supabase Storage en Docker no disponible ({ex.GetType().Name}). Estas pruebas corren en el CI.";
        }
    }

    /// <summary>JWT HS256 con el rol de Supabase (service_role o anon), como las llaves de un proyecto.</summary>
    private static string Jwt(string rol)
    {
        static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var cabecera = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var carga = B64(JsonSerializer.SerializeToUtf8Bytes(new { role = rol, iss = "pruebas", iat = 1700000000, exp = 4100000000 }));
        var firma = B64(HMACSHA256.HashData(Encoding.UTF8.GetBytes(SecretoJwt), Encoding.UTF8.GetBytes($"{cabecera}.{carga}")));
        return $"{cabecera}.{carga}.{firma}";
    }

    public async Task DisposeAsync()
    {
        if (_storage is not null) await _storage.DisposeAsync();
        if (_postgres is not null) await _postgres.DisposeAsync();
        if (_red is not null) await _red.DisposeAsync();
    }
}
