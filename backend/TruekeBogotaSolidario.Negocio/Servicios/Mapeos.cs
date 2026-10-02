using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Comun;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Negocio.Servicios;

internal static class Mapeos
{
    /// <summary>"María Fernanda Pérez Gómez" → "María F." (nunca se expone el nombre completo públicamente).</summary>
    public static string NombrePublico(string nombreCompleto)
    {
        var partes = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length switch
        {
            0 => "Usuario",
            1 => partes[0],
            _ => $"{partes[0]} {char.ToUpperInvariant(partes[1][0])}."
        };
    }

    public static string TipoCuentaEfectivo(Usuario u, DateTime ahora)
        => u.PremiumVigente(ahora) ? nameof(TipoCuenta.Premium) : u.EmpresaVigente(ahora) ? nameof(TipoCuenta.Empresa) : nameof(TipoCuenta.Individual);

    public static PerfilPublicoDto APerfilPublico(Usuario u, DateTime ahora)
        => new(u.Id, NombrePublico(u.NombreCompleto), u.Localidad, u.Reputacion, u.EsVerificado, TipoCuentaEfectivo(u, ahora),
            u.CalificacionPromedio, u.CalificacionesTotal);

    public static PerfilUsuarioDto APerfilUsuario(Usuario u, int publicacionesActivas, DateTime ahora)
        => new(u.Id, NombrePublico(u.NombreCompleto), u.Localidad, u.Reputacion, u.EsVerificado, TipoCuentaEfectivo(u, ahora),
            new DateTime(u.FechaRegistro.Year, u.FechaRegistro.Month, 1, 0, 0, 0, DateTimeKind.Utc), // solo mes y año
            u.TotalTruekesCompletados, u.TotalComprasRealizadas, u.TotalDonacionesRealizadas,
            u.CalificacionPromedio, u.CalificacionesTotal, publicacionesActivas);

    public static UsuarioDto AUsuarioDto(Usuario u, DateTime ahora)
    {
        var planVigente = u.PremiumVigente(ahora) || u.EmpresaVigente(ahora);
        return new UsuarioDto(u.Id, u.NombreCompleto, u.Localidad, u.Correo, u.Rol.ToString(), TipoCuentaEfectivo(u, ahora),
            planVigente ? u.FechaVencimientoSuscripcion : null, u.EsVerificado, u.EstadoVerificacion.ToString(),
            u.SaldoEcoPuntos, u.Reputacion, u.TotalTruekesCompletados, u.TotalComprasRealizadas, u.TotalDonacionesRealizadas,
            u.PremiumVigente(ahora) ? u.DestacadosGratisRestantes : 0,
            u.CorreoVerificado, u.TieneClave, u.GoogleSub is not null, u.DosFactoresActivo, u.CodigosRecuperacionRestantes);
    }

    public static CategoriaDto ACategoriaDto(Categoria c) => new(c.Id, c.NombreCategoria, c.Descripcion);

    /// <summary>Coordenadas exactas solo para dueño, moderadores y el solicitante con solicitud aceptada; al resto ~1 km de imprecisión.</summary>
    public static PublicacionDto APublicacionDto(Publicacion p, Guid? actorId, bool veExacto, bool veModeracion, DateTime ahora, bool esFavorita = false)
    {
        var esMia = actorId.HasValue && p.PropietarioId == actorId.Value;
        double? lat = p.Latitud, lon = p.Longitud;
        var aproximadas = false;
        if (lat.HasValue && lon.HasValue && !veExacto)
        {
            lat = Geo.Aproximar(lat.Value);
            lon = Geo.Aproximar(lon.Value);
            aproximadas = true;
        }
        var vigente = p.EstaDestacadaVigente(ahora);
        var verMotivo = esMia || veModeracion;
        return new PublicacionDto(p.Id, p.Titulo, p.Descripcion, ACategoriaDto(p.Categoria!), p.Modo.ToString(), p.PrecioReferenciaCop,
            p.Localidad, lat, lon, aproximadas, p.Imagenes.OrderBy(i => i.Orden).Select(i => i.Url).ToList(), p.Estado.ToString(),
            p.FechaPublicacion, p.FechaEdicion, vigente,
            vigente ? p.DestacadaHasta : null, APerfilPublico(p.Propietario!, ahora), esMia, esFavorita, p.EstaOculta && verMotivo,
            verMotivo ? p.MotivoOcultamiento : null);
    }
}
