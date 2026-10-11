using Microsoft.Extensions.Caching.Memory;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Negocio.Servicios;

/// <summary>Lo que muestra WhatsApp (u otra red) al compartir una publicación. Solo datos ya públicos en el catálogo.</summary>
/// <param name="Etiqueta">Modo, precio y lugar en una línea: "Compra · $50.000 · Kennedy, Bogotá D.C."</param>
public sealed record VistaPreviaDto(Guid Id, string Titulo, string Descripcion, string Etiqueta, bool TieneFoto);

public interface IVistaPreviaService
{
    /// <summary>null si la publicación no es visible para un visitante anónimo (oculta, reservada, cancelada, de cuenta suspendida…).</summary>
    Task<VistaPreviaDto?> ObtenerAsync(Guid id);
    /// <summary>JPEG de 1200×630 a partir de la foto principal, o null si no hay foto utilizable.</summary>
    Task<byte[]?> ImagenAsync(Guid id, CancellationToken ct = default);
}

public sealed class VistaPreviaService : IVistaPreviaService
{
    public const int LongitudDescripcion = 200;

    /// <summary>
    /// La ruta de la imagen es anónima: sin caché, cada petición descargaría y re-codificaría la foto (CPU y memoria).
    /// Se guarda la tarjeta ya hecha por foto (si la foto cambia, cambia la clave) y se limita cuántas se generan a la vez.
    /// </summary>
    private static readonly TimeSpan VidaTarjeta = TimeSpan.FromHours(6);
    private static readonly SemaphoreSlim Generando = new(2, 2);

    private readonly IPublicacionService _publicaciones;
    private readonly IAlmacenArchivos _almacen;
    private readonly IMemoryCache _cache;

    public VistaPreviaService(IPublicacionService publicaciones, IAlmacenArchivos almacen, IMemoryCache cache)
    {
        _publicaciones = publicaciones; _almacen = almacen; _cache = cache;
    }

    /// <summary>Mismas reglas que el detalle para un visitante anónimo (sin contar la vista: un robot no es una persona).</summary>
    private async Task<Dtos.PublicacionDto?> VisibleAsync(Guid id)
    {
        try { return await _publicaciones.ObtenerAsync(null, id); }
        catch (NoEncontradoException) { return null; }
    }

    public async Task<VistaPreviaDto?> ObtenerAsync(Guid id)
    {
        if (await VisibleAsync(id) is not { } p) return null;
        return new VistaPreviaDto(id, p.Titulo, Resumir(p.Descripcion), Etiqueta(p), FotoPrincipal(p) is not null && _almacen.Habilitado);
    }

    public async Task<byte[]?> ImagenAsync(Guid id, CancellationToken ct = default)
    {
        if (await VisibleAsync(id) is not { } p || FotoPrincipal(p) is not { } url) return null;
        var clave = $"tarjeta:{url}";
        if (_cache.TryGetValue(clave, out byte[]? hecha)) return hecha;

        await Generando.WaitAsync(ct);
        try
        {
            if (_cache.TryGetValue(clave, out hecha)) return hecha; // otra petición la generó mientras esperaba
            byte[]? tarjeta = null;
            if (await _almacen.LeerFotoPublicadaAsync(url, ct) is { } bytes)
            {
                try
                {
                    using var flujo = new MemoryStream(bytes);
                    tarjeta = await ProcesadorImagenes.TarjetaAsync(flujo, ct);
                }
                catch (ReglaDeNegocioException)
                {
                    // foto dañada: se usa la imagen general de la plataforma (y se recuerda, para no reintentar en cada petición)
                }
            }
            _cache.Set(clave, tarjeta, VidaTarjeta);
            return tarjeta;
        }
        finally
        {
            Generando.Release();
        }
    }

    private static string? FotoPrincipal(Dtos.PublicacionDto p) => p.Imagenes.Count > 0 ? p.Imagenes[0] : null;

    private static readonly System.Globalization.CultureInfo Colombia = System.Globalization.CultureInfo.GetCultureInfo("es-CO");

    public static string Etiqueta(Dtos.PublicacionDto p)
    {
        var modo = p.Modo switch { "Donacion" => "Donación", "Compra" => "Compra", _ => "Trueke" };
        var precio = p.Modo == "Compra" && p.PrecioReferenciaCop is { } v ? $" · ${v.ToString("N0", Colombia)}" : "";
        var lugar = string.IsNullOrWhiteSpace(p.Localidad) ? p.Municipio : $"{p.Localidad}, {p.Municipio}";
        return $"{modo}{precio} · {lugar}";
    }

    /// <summary>Una sola línea, máximo 200 caracteres, cortando en un espacio.</summary>
    public static string Resumir(string texto)
    {
        var limpio = string.Join(' ', (texto ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (limpio.Length <= LongitudDescripcion) return limpio;
        var corte = limpio.LastIndexOf(' ', LongitudDescripcion - 1);
        return limpio[..(corte > LongitudDescripcion / 2 ? corte : LongitudDescripcion - 1)].TrimEnd('.', ',', ';', ':') + "…";
    }
}
