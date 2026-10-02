using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

public sealed record IdDto(Guid Id);
public sealed record UsuarioMinDto(Guid Id, string Rol, string Correo);
public sealed record SesionMinDto(string Token, UsuarioMinDto Usuario);

/// <summary>Atajos HTTP para las pruebas de integración.</summary>
public static class Api
{
    public const string ClaveValida = "Clave12345";

    public static async Task<SesionMinDto> RegistrarSesionAsync(WebApplicationFactory<Program> f, string nombre)
    {
        var correo = $"{nombre}-{Guid.NewGuid():N}@trueke.test";
        var resp = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar",
            new { nombreCompleto = $"{nombre} Prueba", localidad = "Chapinero", correo, clave = ClaveValida, aceptoPoliticaDatos = true });
        if (resp.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"Registro falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<SesionMinDto>())!;
    }

    public static async Task<HttpClient> RegistrarAsync(WebApplicationFactory<Program> f, string nombre)
        => ConToken(f, (await RegistrarSesionAsync(f, nombre)).Token);

    public static HttpClient ConToken(WebApplicationFactory<Program> f, string token)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public static async Task<string> LoginAsync(WebApplicationFactory<Program> f, string correo, string clave)
    {
        var resp = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { correo, clave });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<SesionMinDto>())!.Token;
    }

    public static async Task<Guid> CrearPublicacionAsync(HttpClient c, double? lat = null, double? lon = null, string modo = "Trueke")
    {
        var resp = await c.PostAsJsonAsync("/api/v1/publicaciones", new
        {
            titulo = "Bicicleta usada",
            descripcion = "En buen estado",
            categoriaId = 1,
            modo,
            localidad = "Chapinero",
            precioReferenciaCop = modo == "Compra" ? 50000 : (decimal?)null,
            latitud = lat,
            longitud = lon
        });
        if (resp.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"Crear publicación falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<IdDto>())!.Id;
    }
}
