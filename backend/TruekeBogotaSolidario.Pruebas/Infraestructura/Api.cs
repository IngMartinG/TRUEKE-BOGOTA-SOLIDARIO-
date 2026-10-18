using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Contexto;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Correo;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

public sealed record IdDto(Guid Id);
public sealed record UsuarioMinDto(Guid Id, string Rol, string Correo);
public sealed record SesionMinDto(string Token, UsuarioMinDto Usuario);

/// <summary>Atajos HTTP para las pruebas de integración.</summary>
public static class Api
{
    public const string ClaveValida = "Clave12345";
    /// <summary>Mayor de edad: toda cuenta de prueba la registra (la plataforma es solo para mayores de 18 años).</summary>
    public const string FechaNacimientoAdulta = "1990-05-20";

    /// <summary>Nombre completo válido a partir del alias de la prueba ("u1" → "u Prueba"): el nombre solo admite letras.</summary>
    public static string NombreDePrueba(string alias)
    {
        var letras = new string(alias.Where(char.IsLetter).ToArray());
        return (letras.Length == 0 ? "Persona" : letras) + " Prueba";
    }

    /// <summary>
    /// Cuenta lista para operar: correo verificado, foto de perfil (requisitos para publicar, solicitar, chatear y pagar) y
    /// registro de vendedor (para publicar en modo Compra; el flujo sin registro se prueba en RegistroOferentesTests).
    /// </summary>
    public static async Task<SesionMinDto> RegistrarSesionAsync(WebApplicationFactory<Program> f, string nombre)
    {
        var sesion = await RegistrarSesionSinFotoAsync(f, nombre);
        await PonerFotoAsync(f, sesion.Usuario.Id);
        await RegistrarOferenteAsync(ConToken(f, sesion.Token));
        return sesion;
    }

    /// <summary>Datos de vendedor válidos (Ley 1480, art. 53).</summary>
    public static readonly object DatosOferente = new
    {
        tipoDocumento = "CC", documento = "1012345678", nombre = "Persona Prueba", direccion = "Calle 45 # 12-30",
        municipioCodigo = "11001", telefono = "300 123 4567"
    };

    public static async Task RegistrarOferenteAsync(HttpClient c)
    {
        var r = await c.PutAsJsonAsync("/api/v1/usuarios/yo/oferente", DatosOferente);
        if (r.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"Registro de vendedor falló: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
    }

    /// <summary>Correo verificado pero SIN foto de perfil (para probar que la foto es obligatoria).</summary>
    public static async Task<SesionMinDto> RegistrarSesionSinFotoAsync(WebApplicationFactory<Program> f, string nombre)
    {
        var correo = $"{nombre}-{Guid.NewGuid():N}@trueke.test";
        var resp = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/registrar",
            new { nombreCompleto = NombreDePrueba(nombre), localidad = "Chapinero", correo, clave = ClaveValida, fechaNacimiento = FechaNacimientoAdulta, aceptoPoliticaDatos = true, aceptoTerminos = true });
        if (resp.StatusCode != HttpStatusCode.Created)
            throw new InvalidOperationException($"Registro falló: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        await VerificarCorreoAsync(f, correo);
        return (await resp.Content.ReadFromJsonAsync<SesionMinDto>())!;
    }

    /// <summary>Atajo: asigna una foto de perfil directamente en la base (el flujo real de subida se prueba en ImagenesTests).</summary>
    public static async Task PonerFotoAsync(WebApplicationFactory<Program> f, Guid usuarioId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
        (await db.Usuarios.FindAsync(usuarioId))!.CambiarFoto($"{AlmacenFalso.Base}imagenes/{usuarioId:N}/perfil.jpg");
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Atajo: la cuenta sembrada por configuración (SuperUsuario) nace sin fecha de nacimiento y sin aceptar la política y los
    /// términos, como las cuentas anteriores a esas reglas. Para las pruebas que no tratan de eso se registra directo en la base
    /// (los flujos reales se prueban en MayoriaEdadTests y LegalTests).
    /// </summary>
    public static async Task AsegurarMayoriaEdadAsync(WebApplicationFactory<Program> f, string correo)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TruekeDbContext>();
        var legal = scope.ServiceProvider.GetRequiredService<IOptions<LegalOpciones>>().Value;
        var normalizado = Datos.Entidades.Usuario.NormalizarCorreo(correo);
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Correo == normalizado);
        if (u is null) return;
        if (!u.MayoriaEdadConfirmada) u.RegistrarFechaNacimiento(DateOnly.Parse(FechaNacimientoAdulta), DateOnly.FromDateTime(DateTime.UtcNow));
        if (!u.LegalAlDia(legal.VersionPoliticaDatos, legal.VersionTerminos))
        {
            u.AceptarPoliticaDatos(legal.VersionPoliticaDatos, DateTime.UtcNow);
            u.AceptarTerminos(legal.VersionTerminos, DateTime.UtcNow);
        }
        await db.SaveChangesAsync();
    }

    public static CorreoCapturado Correos(WebApplicationFactory<Program> f)
        => (CorreoCapturado)f.Services.GetRequiredService<ICorreoSaliente>();

    /// <summary>Simula que el usuario abre el enlace del correo de confirmación.</summary>
    public static async Task VerificarCorreoAsync(WebApplicationFactory<Program> f, string correo)
    {
        var token = Correos(f).UltimoToken(correo, "verificar-correo") ?? throw new InvalidOperationException("No llegó el correo de verificación.");
        var r = await f.CreateClient().PostAsJsonAsync("/api/v1/auth/verificar-correo", new { token });
        if (r.StatusCode != HttpStatusCode.NoContent)
            throw new InvalidOperationException($"Verificación falló: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
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
        await AsegurarMayoriaEdadAsync(f, correo);
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
            condicion = "Usado",
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
