using Microsoft.Extensions.Options;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Repositorios;
using TruekeBogotaSolidario.Negocio.Archivos;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

public interface IFotoPerfilService
{
    Task<UsuarioDto> CambiarAsync(Guid actorId, string url);
    Task<UsuarioDto> QuitarAsync(Guid actorId);
}

/// <summary>
/// Foto de perfil: misma ruta que las fotos de publicaciones (subida directa con SAS, validación de que el archivo es
/// del usuario y copia limpia sin GPS ni metadatos). La foto anterior se borra del almacenamiento.
/// </summary>
public sealed class FotoPerfilService : IFotoPerfilService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnidadDeTrabajo _uow;
    private readonly IAlmacenArchivos _almacen;
    private readonly UrlsOpciones _urls;
    private readonly TimeProvider _reloj;

    public FotoPerfilService(IUsuarioRepository usuarios, IUnidadDeTrabajo uow, IAlmacenArchivos almacen, IOptions<UrlsOpciones> urls, TimeProvider reloj)
    {
        _usuarios = usuarios; _uow = uow; _almacen = almacen; _urls = urls.Value; _reloj = reloj;
    }

    private async Task<Datos.Entidades.Usuario> CargarAsync(Guid id)
        => await _usuarios.ObtenerPorIdAsync(id) ?? throw new AutenticacionException("Sesión no válida.");

    public async Task<UsuarioDto> CambiarAsync(Guid actorId, string url)
    {
        var u = await CargarAsync(actorId);
        string limpia;
        if (_almacen.Habilitado) limpia = await _almacen.ValidarArchivoPropioAsync(url, actorId, TipoArchivoDto.Imagen);
        else
        {
            ValidadorUrls.ExigirHostPermitido(url, _urls.HostsPermitidosImagenes, "La foto");
            limpia = url;
        }
        var anterior = u.CambiarFoto(limpia);
        await _uow.GuardarCambiosAsync();
        if (anterior is not null && anterior != limpia) await _almacen.EliminarImagenPropiaAsync(anterior, actorId);
        return Mapeos.AUsuarioDto(u, _reloj.GetUtcNow().UtcDateTime);
    }

    public async Task<UsuarioDto> QuitarAsync(Guid actorId)
    {
        var u = await CargarAsync(actorId);
        var anterior = u.QuitarFoto();
        await _uow.GuardarCambiosAsync();
        if (anterior is not null) await _almacen.EliminarImagenPropiaAsync(anterior, actorId);
        return Mapeos.AUsuarioDto(u, _reloj.GetUtcNow().UtcDateTime);
    }
}
