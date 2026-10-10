using TruekeBogotaSolidario.Datos.Common;
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

    public static string TipoCuentaEfectivo(Usuario u, DateTime ahora) => u.PlanEfectivo(ahora).ToString();

    /// <summary>El nombre comercial solo se muestra mientras el plan Empresa esté vigente.</summary>
    private static string? NombreComercialVisible(Usuario u, DateTime ahora) => u.EmpresaVigente(ahora) ? u.NombreComercial : null;

    public static PerfilPublicoDto APerfilPublico(Usuario u, DateTime ahora)
        => new(u.Id, NombrePublico(u.NombreCompleto), u.Localidad, u.Reputacion, u.EsVerificado, TipoCuentaEfectivo(u, ahora),
            u.CalificacionPromedio, u.CalificacionesTotal, Divipola.NombreCompleto(u.MunicipioCodigo), NombreComercialVisible(u, ahora),
            FotoVisible(u));

    /// <summary>Cuentas eliminadas o suspendidas no muestran foto.</summary>
    private static string? FotoVisible(Usuario u) => u.EstaEliminado || u.EstaSuspendido ? null : u.FotoUrl;

    public static PerfilUsuarioDto APerfilUsuario(Usuario u, int publicacionesActivas, DateTime ahora)
        => new(u.Id, NombrePublico(u.NombreCompleto), u.Localidad, u.Reputacion, u.EsVerificado, TipoCuentaEfectivo(u, ahora),
            new DateTime(u.FechaRegistro.Year, u.FechaRegistro.Month, 1, 0, 0, 0, DateTimeKind.Utc), // solo mes y año
            u.TotalTruekesCompletados, u.TotalComprasRealizadas, u.TotalDonacionesRealizadas,
            u.CalificacionPromedio, u.CalificacionesTotal, publicacionesActivas, Divipola.NombreCompleto(u.MunicipioCodigo),
            NombreComercialVisible(u, ahora), FotoVisible(u));

    public static UsuarioDto AUsuarioDto(Usuario u, DateTime ahora)
    {
        var planVigente = u.PlanPagoVigente(ahora);
        return new UsuarioDto(u.Id, u.NombreCompleto, u.Localidad, u.Correo, u.Rol.ToString(), TipoCuentaEfectivo(u, ahora),
            planVigente ? u.FechaVencimientoSuscripcion : null, u.EsVerificado, u.EstadoVerificacion.ToString(),
            u.SaldoEcoPuntos, u.Reputacion, u.TotalTruekesCompletados, u.TotalComprasRealizadas, u.TotalDonacionesRealizadas,
            planVigente ? u.DestacadosGratisRestantes : 0,
            u.CorreoVerificado, u.TieneClave, u.GoogleSub is not null, u.DosFactoresActivo, u.CodigosRecuperacionRestantes,
            u.MunicipioCodigo, Divipola.NombreCompleto(u.MunicipioCodigo), u.NombreComercial, u.Nit, u.TieneDatosFacturacion, u.FotoUrl);
    }

    public static CategoriaDto ACategoriaDto(Categoria c) => new(c.Id, c.NombreCategoria, c.Descripcion);

    /// <summary>
    /// Coordenadas exactas solo para dueño, moderadores y el solicitante con solicitud aceptada; al resto ~1 km de imprecisión.
    /// <paramref name="vistas"/> (rendimiento) y el próximo impulso solo se envían al dueño.
    /// </summary>
    public static PublicacionDto APublicacionDto(Publicacion p, Guid? actorId, bool veExacto, bool veModeracion, DateTime ahora,
        bool esFavorita = false, int? vistas = null, int interesados = 0, bool yaSolicite = false)
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
            verMotivo ? p.MotivoOcultamiento : null,
            p.Condicion.ToString(), p.DetalleCondicion, p.MunicipioCodigo, Divipola.NombreCompleto(p.MunicipioCodigo), p.DepartamentoCodigo,
            esMia ? vistas ?? 0 : null, esMia ? p.ProximoImpulsoPosible : null,
            p.Estado == EstadoPublicacionEnum.Disponible ? interesados : 0, yaSolicite);
    }

    public static DatosFacturacionDto ADatosFacturacion(Usuario u)
        => new(u.TieneDatosFacturacion, u.FacturacionTipoDocumento?.ToString(), u.FacturacionDocumento, u.FacturacionNombre,
            u.FacturacionCorreo, u.FacturacionDireccion, u.FacturacionMunicipioCodigo,
            u.FacturacionMunicipioCodigo is null ? null : Divipola.NombreCompleto(u.FacturacionMunicipioCodigo));

    public static FacturaDto AFacturaDto(Factura f)
        => new(f.Id, f.Referencia, f.Concepto.ToString(), f.Descripcion, f.FechaUtc, f.TotalCop, f.BaseCop, f.IvaCop, f.IvaPorcentaje,
            f.Estado.ToString(), f.NumeroDian, f.Cufe, f.FechaEmisionUtc, f.CompradorNombre, f.CompradorDocumento);

    public static FacturaAdminDto AFacturaAdminDto(Factura f)
        => new(f.Id, f.Referencia, f.UsuarioId, f.Concepto.ToString(), f.Descripcion, f.FechaUtc, f.TotalCop, f.BaseCop, f.IvaCop,
            f.IvaPorcentaje, f.Estado.ToString(), f.NumeroDian, f.Cufe, f.FechaEmisionUtc, f.CompradorTipoDocumento?.ToString(),
            f.CompradorDocumento, f.CompradorNombre, f.CompradorCorreo, f.CompradorDireccion,
            f.CompradorMunicipioCodigo is null ? null : Divipola.NombreCompleto(f.CompradorMunicipioCodigo), f.RequiereNotaCredito, f.NotaInterna);

    public static PqrDto APqrDto(Pqr p)
        => new(p.Id, p.Radicado, p.Tipo.ToString(), p.Asunto, p.Descripcion, p.PagoReferencia, p.Estado.ToString(), p.FechaUtc,
            p.FechaLimiteUtc, p.Respuesta, p.FechaRespuestaUtc);

    /// <summary>Texto que aparece en la factura para cada concepto.</summary>
    public static string DescripcionConcepto(ConceptoPago c) => c switch
    {
        ConceptoPago.Destacar => $"Publicación destacada por {PoliticaEcoPuntos.DuracionDestacadoDias} días",
        ConceptoPago.Verificar => "Verificación de identidad de la cuenta",
        ConceptoPago.Premium => $"Plan Premium ({PoliticaEcoPuntos.DuracionSuscripcionDias} días)",
        ConceptoPago.Empresa => $"Plan Empresa ({PoliticaEcoPuntos.DuracionSuscripcionDias} días)",
        ConceptoPago.Recarga => "Recarga de Eco-Puntos (crédito de servicios en la plataforma)",
        _ => c.ToString()
    };
}
