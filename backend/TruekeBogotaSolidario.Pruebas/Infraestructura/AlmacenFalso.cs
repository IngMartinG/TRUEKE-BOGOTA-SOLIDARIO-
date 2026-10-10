using System.Collections.Concurrent;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.Infraestructura;

/// <summary>Almacén en memoria con las MISMAS reglas que el de Azure (ReglasArchivos). Subir() simula el PUT del navegador.</summary>
public sealed class AlmacenFalso : IAlmacenArchivos
{
    public static readonly Uri Base = new("https://almacen.test/");
    private readonly ConcurrentDictionary<string, (string ContentType, byte[] Datos)> _blobs = new();

    public bool Habilitado => true;

    private static Uri Contenedor(TipoArchivoDto tipo) => new(Base, tipo == TipoArchivoDto.Documento ? "documentos" : "imagenes");

    public Task<SubidaArchivoDto> CrearSubidaAsync(Guid usuarioId, TipoArchivoDto tipo, string contentType, long tamanoBytes, CancellationToken ct = default)
    {
        var ext = ReglasArchivos.Extension(tipo, contentType);
        ReglasArchivos.ValidarTamano(tamanoBytes);
        var url = $"{Contenedor(tipo)}/{ReglasArchivos.NuevoNombre(usuarioId, ext)}";
        return Task.FromResult(new SubidaArchivoDto(url + "?sas=falsa", url, "PUT",
            new Dictionary<string, string> { ["Content-Type"] = contentType }, DateTime.UtcNow.AddMinutes(5)));
    }

    public void Subir(string url, string contentType, byte[] datos) => _blobs[url] = (contentType, datos);

    public Task<string> GuardarImagenAsync(Guid usuarioId, byte[] contenido, CancellationToken ct = default)
    {
        ReglasArchivos.ValidarTamano(contenido.Length);
        var url = $"{Contenedor(TipoArchivoDto.Imagen)}/{ReglasArchivos.NuevoNombre(usuarioId, "jpg")}";
        _blobs[url] = ("image/jpeg", contenido);
        return Task.FromResult(url);
    }

    /// <summary>Mismas validaciones que Azure; no re-codifica (eso se prueba con imágenes reales en ProcesadorImagenesTests).</summary>
    public Task<string> ValidarArchivoPropioAsync(string url, Guid usuarioId, TipoArchivoDto tipo, CancellationToken ct = default)
    {
        var a = ReglasArchivos.AnalizarUrl(url, Contenedor(tipo), usuarioId)
                ?? throw new ReglaDeNegocioException("El archivo debe subirse primero con POST /api/v1/archivos/subidas.");
        if (!_blobs.TryGetValue(url, out var b)) throw new ReglaDeNegocioException("El archivo no existe o aún no terminó de subirse.");
        ReglasArchivos.ValidarTamano(b.Datos.Length);
        if (ReglasArchivos.Extension(tipo, b.ContentType) != a.Extension) throw new ReglaDeNegocioException("El tipo del archivo no coincide con su extensión.");
        if (!ReglasArchivos.FirmaCoincide(a.Extension, b.Datos.AsSpan(0, Math.Min(ReglasArchivos.BytesFirma, b.Datos.Length))))
            throw new ReglaDeNegocioException("El contenido del archivo no corresponde a una imagen o documento válido.");
        return Task.FromResult(url);
    }

    public Task<string?> UrlLecturaTemporalAsync(string url, CancellationToken ct = default) => Task.FromResult<string?>(url + "?sas=lectura");

    public List<Guid> Eliminados { get; } = new();
    public Task EliminarDelUsuarioAsync(Guid usuarioId, CancellationToken ct = default)
    {
        lock (Eliminados) Eliminados.Add(usuarioId);
        foreach (var k in _blobs.Keys.Where(k => k.Contains($"/{usuarioId:N}/", StringComparison.Ordinal)).ToList()) _blobs.TryRemove(k, out _);
        return Task.CompletedTask;
    }

    public Task EliminarDocumentoAsync(string url, CancellationToken ct = default)
    {
        _blobs.TryRemove(url, out _);
        return Task.CompletedTask;
    }

    public Task EliminarImagenPropiaAsync(string url, Guid usuarioId, CancellationToken ct = default)
    {
        if (ReglasArchivos.AnalizarUrl(url, Contenedor(TipoArchivoDto.Imagen), usuarioId) is not null) _blobs.TryRemove(url, out _);
        return Task.CompletedTask;
    }

    public bool Existe(string url) => _blobs.ContainsKey(url);

    public static byte[] Png(int tamano = 100)
    {
        var d = new byte[tamano];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(d, 0);
        return d;
    }
}
