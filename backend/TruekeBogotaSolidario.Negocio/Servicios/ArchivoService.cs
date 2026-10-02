using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IArchivoService
{
    Task<SubidaArchivoDto> SolicitarSubidaAsync(Guid actorId, SolicitarSubidaRequest r, CancellationToken ct = default);
}

public sealed class ArchivoService : IArchivoService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IAlmacenArchivos _almacen;

    public ArchivoService(IUsuarioRepository usuarios, IAlmacenArchivos almacen)
    {
        _usuarios = usuarios; _almacen = almacen;
    }

    public async Task<SubidaArchivoDto> SolicitarSubidaAsync(Guid actorId, SolicitarSubidaRequest r, CancellationToken ct = default)
    {
        var u = await _usuarios.ObtenerPorIdAsync(actorId) ?? throw new AutenticacionException("Sesión no válida.");
        Guardas.ExigirCorreoVerificado(u);
        return await _almacen.CrearSubidaAsync(actorId, r.Tipo, r.ContentType, r.TamanoBytes, ct);
    }
}
