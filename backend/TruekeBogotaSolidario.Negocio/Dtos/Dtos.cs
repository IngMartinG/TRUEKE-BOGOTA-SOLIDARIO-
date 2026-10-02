using System.ComponentModel.DataAnnotations;

namespace TruekeBogotaSolidario.Negocio.Dtos;

// Enums propios de la capa de negocio (mismos valores numéricos que Datos). El usuario actuante NUNCA viaja en un request: sale del JWT.
public enum ModoDto { Trueke = 1, Compra = 2, Donacion = 3 }
public enum RolDto { Cliente = 1, Administrador = 2, SuperUsuario = 3 }
public enum ConceptoPagoDto { Destacar = 1, Verificar = 2, Premium = 3, Empresa = 4, Recarga = 5 }

// ---------------- Requests ----------------
public sealed class RegistroRequest
{
    [Required, StringLength(120, MinimumLength = 3)] public string NombreCompleto { get; init; } = "";
    [Required, StringLength(60, MinimumLength = 2)] public string Localidad { get; init; } = "";
    [Required, EmailAddress, MaxLength(160)] public string Correo { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string Clave { get; init; } = "";
    /// <summary>Ley 1581 de 2012: autorización expresa para el tratamiento de datos personales.</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Debes aceptar la política de tratamiento de datos personales.")]
    public bool AceptoPoliticaDatos { get; init; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(160)] public string Correo { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 1)] public string Clave { get; init; } = "";
}

public sealed class CambiarClaveRequest
{
    /// <summary>Obligatoria si la cuenta ya tiene contraseña; las cuentas creadas solo con Google pueden omitirla para crear una.</summary>
    [MaxLength(128)] public string? ClaveActual { get; init; }
    [Required, StringLength(128, MinimumLength = 8)] public string ClaveNueva { get; init; } = "";
}

public sealed class TokenRequest
{
    [Required, StringLength(100, MinimumLength = 20)] public string Token { get; init; } = "";
}

public sealed class OlvideClaveRequest
{
    [Required, EmailAddress, MaxLength(160)] public string Correo { get; init; } = "";
}

public sealed class RestablecerClaveRequest
{
    [Required, StringLength(100, MinimumLength = 20)] public string Token { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string ClaveNueva { get; init; } = "";
}

public sealed class ActualizarPerfilRequest
{
    [Required, StringLength(120, MinimumLength = 3)] public string NombreCompleto { get; init; } = "";
    [Required, StringLength(60, MinimumLength = 2)] public string Localidad { get; init; } = "";
}

public sealed class CrearPublicacionRequest
{
    [Required, StringLength(120, MinimumLength = 3)] public string Titulo { get; init; } = "";
    [Required, StringLength(2000, MinimumLength = 1)] public string Descripcion { get; init; } = "";
    [Range(1, int.MaxValue)] public int CategoriaId { get; init; }
    [EnumDataType(typeof(ModoDto))] public ModoDto Modo { get; init; }
    [Required, StringLength(60, MinimumLength = 2)] public string Localidad { get; init; } = "";
    [Range(0.01, 1_000_000_000)] public decimal? PrecioReferenciaCop { get; init; }
    [Range(-90, 90)] public double? Latitud { get; init; }
    [Range(-180, 180)] public double? Longitud { get; init; }
    [MaxLength(500)] public string? ImagenUrl { get; init; }
}

public sealed class CancelarPublicacionRequest
{
    [MaxLength(300)] public string? Motivo { get; init; }
}

public sealed class MotivoRequest
{
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; init; } = "";
}

public sealed class CrearSolicitudRequest
{
    public Guid PublicacionId { get; init; }
    [Required, StringLength(500, MinimumLength = 1)] public string Mensaje { get; init; } = "";
}

public sealed class RechazarSolicitudRequest
{
    [MaxLength(300)] public string? Motivo { get; init; }
}

public sealed class CambiarRolRequest
{
    [EnumDataType(typeof(RolDto))] public RolDto Rol { get; init; }
}

public sealed class IniciarPagoRequest
{
    [EnumDataType(typeof(ConceptoPagoDto))] public ConceptoPagoDto Concepto { get; init; }
    public Guid? PublicacionId { get; init; }
    [MaxLength(500)] public string? DocumentoUrl { get; init; }
    [Range(1, 10_000_000)] public int? MontoRecargaCop { get; init; }
}

public sealed class FiltroPublicacionesRequest
{
    public int? CategoriaId { get; init; }
    public ModoDto? Modo { get; init; }
    [MaxLength(60)] public string? Localidad { get; init; }
    [MaxLength(100)] public string? Texto { get; init; }
    [Range(1, 10_000)] public int Pagina { get; init; } = 1;
    [Range(1, 50)] public int Tamano { get; init; } = 20;
}

public sealed class CercanasRequest
{
    [Required, Range(-90, 90)] public double? Lat { get; init; }
    [Required, Range(-180, 180)] public double? Lon { get; init; }
    [Range(0.1, 50)] public double RadioKm { get; init; } = 5;
    public int? CategoriaId { get; init; }
    public ModoDto? Modo { get; init; }
    [Range(1, 50)] public int Max { get; init; } = 20;
}

public sealed class CrearComentarioRequest
{
    [Required, StringLength(500, MinimumLength = 1)] public string Texto { get; init; } = "";
}

public sealed class PaginacionRequest
{
    [Range(1, 10_000)] public int Pagina { get; init; } = 1;
    [Range(1, 50)] public int Tamano { get; init; } = 20;
}

// ---------------- Responses ----------------
public sealed record UsuarioDto(Guid Id, string NombreCompleto, string Localidad, string Correo, string Rol, string TipoCuenta,
    DateTime? PlanVigenteHasta, bool Verificado, string EstadoVerificacion, int SaldoEcoPuntos, decimal Reputacion,
    int TruekesCompletados, int ComprasRealizadas, int DonacionesRealizadas, int DestacadosGratisRestantes,
    bool CorreoVerificado, bool TieneClave, bool VinculadoGoogle);

public sealed record SesionDto(string Token, DateTime ExpiraUtc, UsuarioDto Usuario);

/// <summary>
/// Resultado interno de autenticarse. Presentacion devuelve SOLO <see cref="Sesion"/> en el JSON y guarda
/// <see cref="TokenRefresco"/> en una cookie HttpOnly: el refresco nunca es accesible desde JavaScript.
/// </summary>
public sealed record ResultadoAutenticacion(SesionDto Sesion, string TokenRefresco, DateTime TokenRefrescoExpiraUtc);

public sealed record PerfilPublicoDto(string Nombre, string Localidad, decimal Reputacion, bool Verificado, string TipoCuenta);

public sealed record CategoriaDto(int Id, string Nombre, string Descripcion);

public sealed record PublicacionDto(Guid Id, string Titulo, string Descripcion, CategoriaDto Categoria, string Modo,
    decimal? PrecioReferenciaCop, string Localidad, double? Latitud, double? Longitud, bool CoordenadasAproximadas,
    string? ImagenUrl, string Estado, DateTime FechaPublicacion, bool Destacada, DateTime? DestacadaHasta,
    PerfilPublicoDto Propietario, bool EsMia, bool Oculta, string? MotivoOcultamiento);

/// <summary>DistanciaKm se calcula con las coordenadas que el usuario tiene permitido ver (aproximadas para terceros), redondeada a 0,1 km.</summary>
public sealed record PublicacionCercanaDto(PublicacionDto Publicacion, double DistanciaKm);

/// <summary>Oculto y MotivoOcultamiento solo tienen valor para moderadores (el público nunca recibe comentarios ocultos).</summary>
public sealed record ComentarioDto(Guid Id, Guid PublicacionId, PerfilPublicoDto Autor, string Texto, DateTime FechaUtc,
    bool EsMio, bool Oculto, string? MotivoOcultamiento);

public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Total, int Pagina, int Tamano);

public sealed record SolicitudDto(Guid Id, Guid PublicacionId, string PublicacionTitulo, string Modo, PerfilPublicoDto Solicitante,
    DateTime FechaSolicitud, string Mensaje, string Estado, string? MotivoRechazo, string? CorreoContacto);

public sealed record VerificacionPendienteDto(Guid UsuarioId, string NombreCompleto, string Correo, string DocumentoUrl, DateTime FechaRegistro);

public sealed record EcoPuntosResumenDto(int Saldo, decimal Reputacion, int TransaccionesConPuntosEnUltimas24h, int TransaccionesConPuntosRestantes,
    string TipoCuenta, DateTime? PlanVigenteHasta, int DestacadosGratisRestantes);

public sealed record CotizacionDto(int PrecioBaseCop, int DescuentoPorcentaje, int PuntosACanjear, int TotalCop);

public sealed record PagoIniciadoDto(string Referencia, string Concepto, int MontoCop, long MontoEnCentavos, string Moneda,
    string Proveedor, string? LlavePublica, string? FirmaIntegridad, CotizacionDto? Cotizacion, DateTime ExpiraUtc);

public sealed record PagoEstadoDto(string Referencia, string Concepto, int MontoCop, string Estado, DateTime FechaUtc, DateTime? FechaResolucionUtc);

public sealed record EscalonDto(int PuntosMinimos, int DescuentoPorcentaje);
public sealed record GananciaDto(string Modo, int Puntos, decimal Reputacion);
public sealed record PoliticaEcoPuntosDto(
    int PuntosBienvenida, IReadOnlyList<GananciaDto> Ganancias, int MaxTransaccionesConPuntosPorDia, decimal ReputacionMaxima,
    int PrecioDestacarCop, int DuracionDestacadoDias, IReadOnlyList<EscalonDto> EscalonesDestacar,
    int PrecioVerificarCop, IReadOnlyList<EscalonDto> EscalonesVerificar,
    int PrecioPremiumCop, int DestacadosGratisPremium, int DescuentoPremiumPorcentaje,
    int PrecioEmpresaCop, int DuracionSuscripcionDias,
    int CopPorEcoPunto, int RecargaMinimaCop, int RecargaMaximaCop);
