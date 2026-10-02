using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>
/// Revocación de sesiones: cada JWT lleva la "versión de seguridad" del usuario. Si cambia su rol o su contraseña,
/// o cierra sesiones, todos los tokens anteriores dejan de ser válidos (con una caché corta para no golpear la BD en cada request).
/// </summary>
public interface ISesionService
{
    Task<bool> EsVigenteAsync(Guid usuarioId, int version);
    void Invalidar(Guid usuarioId);
}

public sealed class SesionService : ISesionService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IMemoryCache _cache;
    private readonly SeguridadOpciones _opciones;

    public SesionService(IUsuarioRepository usuarios, IMemoryCache cache, IOptions<SeguridadOpciones> opciones)
    {
        _usuarios = usuarios;
        _cache = cache;
        _opciones = opciones.Value;
    }

    private static string Clave(Guid id) => $"sv:{id:N}";

    public async Task<bool> EsVigenteAsync(Guid usuarioId, int version)
    {
        if (_cache.TryGetValue(Clave(usuarioId), out int? enCache))
            return enCache == version;

        var actual = await _usuarios.ObtenerVersionSeguridadAsync(usuarioId); // null = usuario inexistente
        if (_opciones.SegundosCacheSesion > 0)
            _cache.Set(Clave(usuarioId), actual, TimeSpan.FromSeconds(_opciones.SegundosCacheSesion));
        return actual == version;
    }

    public void Invalidar(Guid usuarioId) => _cache.Remove(Clave(usuarioId));
}
