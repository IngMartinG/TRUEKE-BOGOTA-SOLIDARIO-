using System.ComponentModel.DataAnnotations;
using TruekeBogotaSolidario.Negocio.Comun;

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

public sealed class GoogleLoginRequest
{
    /// <summary>ID token (JWT) que entrega Google Identity Services en el navegador.</summary>
    [Required, StringLength(4096, MinimumLength = 20)] public string IdToken { get; init; } = "";
    /// <summary>Obligatorio solo si la cuenta es nueva (Ley 1581).</summary>
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
    /// <summary>Hasta 5 URLs obtenidas con POST /archivos/subidas. La primera es la foto principal. Al editar, reemplaza la lista completa.</summary>
    [MaxLength(5)] public List<string>? Imagenes { get; init; }
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

public enum OrdenPublicacionesDto { Recientes = 1, PrecioAsc = 2, PrecioDesc = 3 }

public sealed class FiltroPublicacionesRequest
{
    public int? CategoriaId { get; init; }
    public ModoDto? Modo { get; init; }
    [MaxLength(60)] public string? Localidad { get; init; }
    [MaxLength(100)] public string? Texto { get; init; }
    [Range(0, 1_000_000_000)] public decimal? PrecioMin { get; init; }
    [Range(0, 1_000_000_000)] public decimal? PrecioMax { get; init; }
    /// <summary>Solo publicaciones de cuentas con identidad verificada.</summary>
    public bool SoloVerificados { get; init; }
    /// <summary>Recientes (destacadas primero), PrecioAsc o PrecioDesc. Para ordenar por cercanía usa /publicaciones/cercanas.</summary>
    [EnumDataType(typeof(OrdenPublicacionesDto))] public OrdenPublicacionesDto Orden { get; init; } = OrdenPublicacionesDto.Recientes;
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

public sealed class EnviarMensajeRequest
{
    [Required, StringLength(1000, MinimumLength = 1)] public string Texto { get; init; } = "";
}

public sealed class MensajesRequest
{
    /// <summary>Cursor: devuelve mensajes anteriores a esta fecha (para "cargar más").</summary>
    public DateTime? AntesDe { get; init; }
    [Range(1, 100)] public int Tamano { get; init; } = 30;
}

public enum TipoArchivoDto { Imagen = 1, Documento = 2 }

public sealed class SolicitarSubidaRequest
{
    /// <summary>Imagen (publicaciones, contenedor público) o Documento (verificación de identidad, contenedor PRIVADO).</summary>
    [EnumDataType(typeof(TipoArchivoDto))] public TipoArchivoDto Tipo { get; init; } = TipoArchivoDto.Imagen;
    [Required, StringLength(50)] public string ContentType { get; init; } = "";
    [Range(1, 5 * 1024 * 1024)] public long TamanoBytes { get; init; }
}

public enum TipoDenunciaDto { Publicacion = 1, Comentario = 2, Mensaje = 3, Usuario = 4, Calificacion = 5 }
public enum MotivoDenunciaDto { Spam = 1, Fraude = 2, ContenidoInapropiado = 3, ArticuloProhibido = 4, Acoso = 5, Otro = 6 }
public enum EstadoDenunciaDto { Pendiente = 1, Resuelta = 2, Descartada = 3 }
public enum AccionDenunciaDto { Descartar = 1, OcultarContenido = 2, MarcarRevisada = 3 }

public sealed class CrearDenunciaRequest
{
    [EnumDataType(typeof(TipoDenunciaDto))] public TipoDenunciaDto Tipo { get; init; }
    public Guid ObjetivoId { get; init; }
    [EnumDataType(typeof(MotivoDenunciaDto))] public MotivoDenunciaDto Motivo { get; init; }
    [MaxLength(500)] public string? Detalle { get; init; }
}

public sealed class FiltroDenunciasRequest
{
    [EnumDataType(typeof(EstadoDenunciaDto))] public EstadoDenunciaDto Estado { get; init; } = EstadoDenunciaDto.Pendiente;
}

public sealed class ResolverDenunciaRequest
{
    /// <summary>OcultarContenido: oculta la publicación, comentario o mensaje. MarcarRevisada: procedente sin ocultar (p. ej. denuncias a usuarios).</summary>
    [EnumDataType(typeof(AccionDenunciaDto))] public AccionDenunciaDto Accion { get; init; }
    [StringLength(300, MinimumLength = 3)] public string? Nota { get; init; }
}

/// <summary>Derecho de supresión (Ley 1581). Exige volver a demostrar identidad: la clave, o un ID token de Google si la cuenta no tiene clave.</summary>
public sealed class EliminarCuentaRequest
{
    [Required, RegularExpression("^ELIMINAR$", ErrorMessage = "Escribe ELIMINAR para confirmar.")]
    public string Confirmacion { get; init; } = "";
    [MaxLength(128)] public string? Clave { get; init; }
    [StringLength(4096)] public string? GoogleIdToken { get; init; }
}

// ---------------- Administración ----------------
public sealed class SuspenderUsuarioRequest
{
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; init; } = "";
    /// <summary>Días de suspensión; null = indefinida (hasta reactivar).</summary>
    [Range(1, 3650)] public int? Dias { get; init; }
}

public sealed class BuscarUsuariosRequest
{
    [MaxLength(100)] public string? Texto { get; init; }
    public bool SoloSuspendidos { get; init; }
    [Range(1, 10_000)] public int Pagina { get; init; } = 1;
    [Range(1, 50)] public int Tamano { get; init; } = 20;
}

public enum EstadoPagoDto { Pendiente = 1, Aprobado = 2, Rechazado = 3, Expirado = 4, RequiereRevision = 5, Reembolsado = 6 }

public sealed class FiltroPagosAdminRequest
{
    [EnumDataType(typeof(EstadoPagoDto))] public EstadoPagoDto Estado { get; init; } = EstadoPagoDto.RequiereRevision;
    [Range(1, 10_000)] public int Pagina { get; init; } = 1;
    [Range(1, 50)] public int Tamano { get; init; } = 20;
}

public sealed class ReembolsoRequest
{
    /// <summary>Referencia o nota del reembolso hecho en el panel de Wompi.</summary>
    [Required, StringLength(300, MinimumLength = 3)] public string Nota { get; init; } = "";
}

public sealed class FiltroNotificacionesRequest
{
    public bool SoloNoLeidas { get; init; }
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
public sealed record ResultadoAutenticacion(SesionDto Sesion, string TokenRefresco, DateTime TokenRefrescoExpiraUtc)
{
    /// <summary>true si el inicio de sesión creó la cuenta (Google): Presentacion responde 201.</summary>
    public bool CuentaCreada { get; init; }
}

/// <summary>
/// Datos públicos de una persona. Id permite abrir su perfil (GET /usuarios/{id}/perfil); no da acceso a nada:
/// toda acción usa el usuario del JWT. Nunca incluye correo, nombre completo ni ubicación exacta.
/// </summary>
public sealed record PerfilPublicoDto(Guid Id, string Nombre, string Localidad, decimal Reputacion, bool Verificado, string TipoCuenta,
    decimal? CalificacionPromedio, int TotalCalificaciones);

public sealed record PerfilUsuarioDto(Guid Id, string Nombre, string Localidad, decimal Reputacion, bool Verificado, string TipoCuenta,
    DateTime MiembroDesde, int TruekesCompletados, int ComprasRealizadas, int DonacionesRealizadas,
    decimal? CalificacionPromedio, int TotalCalificaciones, int PublicacionesActivas);

public sealed record CategoriaDto(int Id, string Nombre, string Descripcion);

/// <summary>Imagenes[0] es la foto principal. EsFavorita solo es true para el usuario autenticado que la guardó.</summary>
public sealed record PublicacionDto(Guid Id, string Titulo, string Descripcion, CategoriaDto Categoria, string Modo,
    decimal? PrecioReferenciaCop, string Localidad, double? Latitud, double? Longitud, bool CoordenadasAproximadas,
    IReadOnlyList<string> Imagenes, string Estado, DateTime FechaPublicacion, DateTime? FechaEdicion, bool Destacada, DateTime? DestacadaHasta,
    PerfilPublicoDto Propietario, bool EsMia, bool EsFavorita, bool Oculta, string? MotivoOcultamiento);

/// <summary>DistanciaKm se calcula con las coordenadas que el usuario tiene permitido ver (aproximadas para terceros), redondeada a 0,1 km.</summary>
public sealed record PublicacionCercanaDto(PublicacionDto Publicacion, double DistanciaKm);

/// <summary>Oculto y MotivoOcultamiento solo tienen valor para moderadores (el público nunca recibe comentarios ocultos).</summary>
public sealed record ComentarioDto(Guid Id, Guid PublicacionId, PerfilPublicoDto Autor, string Texto, DateTime FechaUtc,
    bool EsMio, bool Oculto, string? MotivoOcultamiento);

/// <summary>Mensaje de chat. EsMio es relativo a quien lo recibe. Si fue ocultado por moderación, Texto trae un aviso.</summary>
public sealed record MensajeChatDto(Guid Id, Guid ConversacionId, bool EsMio, string Texto, DateTime FechaUtc, bool Leido, bool Oculto);

/// <summary>Resumen de una conversación para la bandeja de chats.</summary>
public sealed record ConversacionDto(Guid Id, Guid SolicitudId, Guid PublicacionId, string PublicacionTitulo, string EstadoSolicitud,
    bool SoyDuenio, PerfilPublicoDto Contraparte, string? UltimoMensaje, DateTime UltimoMensajeUtc, int NoLeidos, bool Escribible);

/// <summary>
/// Autorización de subida directa a Azure Blob: el navegador hace PUT del archivo a <see cref="UrlSubida"/> con
/// <see cref="Cabeceras"/> antes de <see cref="ExpiraUtc"/>, y luego envía <see cref="UrlArchivo"/> a la API.
/// </summary>
public sealed record SubidaArchivoDto(string UrlSubida, string UrlArchivo, string Metodo, IReadOnlyDictionary<string, string> Cabeceras, DateTime ExpiraUtc);

/// <summary>Vista de administración (incluye el correo: solo la reciben moderadores).</summary>
public sealed record UsuarioAdminDto(Guid Id, string NombreCompleto, string Correo, string Localidad, string Rol, DateTime FechaRegistro,
    bool CorreoVerificado, string EstadoVerificacion, bool Suspendido, DateTime? SuspendidoHasta, string? MotivoSuspension,
    bool DosFactoresActivo, decimal Reputacion, decimal? CalificacionPromedio, int TotalCalificaciones);

public sealed record PagoAdminDto(string Referencia, Guid UsuarioId, string Concepto, int MontoCop, int PuntosCanjeados, string Estado,
    DateTime FechaUtc, DateTime? FechaResolucionUtc, string? ProveedorTransaccionId, string? NotaInterna);

public sealed record DenunciaCreadaDto(Guid Id, string Estado, DateTime FechaUtc);

/// <summary>
/// Cola de moderación: denuncias agrupadas por objetivo. <see cref="DenunciaId"/> es la más antigua del grupo (resolverla
/// resuelve todas). VistaPrevia muestra lo denunciado SOLO al moderador; Detalles son los textos de los denunciantes.
/// </summary>
public sealed record DenunciaAgrupadaDto(Guid DenunciaId, string Tipo, Guid ObjetivoId, int Total, IReadOnlyList<string> Motivos,
    IReadOnlyList<string> Detalles, string? VistaPrevia, bool ObjetivoExiste, DateTime PrimeraUtc, DateTime UltimaUtc, string Estado);

// ---------------- Exportación de datos personales (Ley 1581: derecho de acceso) ----------------
public sealed record ComentarioExportDto(Guid Id, Guid PublicacionId, string Texto, DateTime FechaUtc, bool Oculto);
public sealed record MensajeExportDto(Guid Id, Guid ConversacionId, string Texto, DateTime FechaUtc);
public sealed record TransaccionExportDto(Guid Id, Guid PublicacionId, string Modo, string MiRol, bool RecibiPuntos, DateTime FechaUtc);
public sealed record DenunciaExportDto(Guid Id, string Tipo, Guid ObjetivoId, string Motivo, string? Detalle, string Estado, DateTime FechaUtc);

public sealed record DatosPersonalesDto(DateTime GeneradoUtc, UsuarioDto Perfil, string? PoliticaDatosVersion, DateTime? FechaAceptacionPolitica,
    IReadOnlyList<PublicacionDto> Publicaciones, IReadOnlyList<SolicitudDto> SolicitudesEnviadas, IReadOnlyList<ComentarioExportDto> Comentarios,
    IReadOnlyList<MensajeExportDto> MensajesEnviados, IReadOnlyList<TransaccionExportDto> Transacciones, IReadOnlyList<PagoEstadoDto> Pagos,
    IReadOnlyList<NotificacionDto> Notificaciones, IReadOnlyList<DenunciaExportDto> DenunciasRealizadas,
    IReadOnlyList<CalificacionExportDto> CalificacionesRealizadas, IReadOnlyList<Guid> Favoritos);

public sealed record CalificacionExportDto(Guid Id, Guid SolicitudId, int Estrellas, string? Comentario, DateTime FechaUtc);

public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Total, int Pagina, int Tamano);

/// <summary>
/// Las partes se comunican por el chat (ConversacionId): la API nunca comparte correos entre usuarios.
/// Estado: Pendiente → Aceptada (coordinando la entrega) → Completada | NoConcretada; o Rechazada | Cancelada.
/// CierreAutomaticoUtc: si sigue Aceptada, cuándo se cerrará sola (completada si alguien confirmó; no concretada si nadie).
/// </summary>
public sealed record SolicitudDto(Guid Id, Guid PublicacionId, string PublicacionTitulo, string Modo, PerfilPublicoDto Propietario,
    PerfilPublicoDto Solicitante, bool SoyDuenio, DateTime FechaSolicitud, string Mensaje, string Estado, string? MotivoRechazo,
    Guid? ConversacionId, DateTime? FechaAceptacionUtc, bool ConfirmadaPorDuenio, bool ConfirmadaPorSolicitante,
    DateTime? FechaCierreUtc, DateTime? CierreAutomaticoUtc, bool PuedoCalificar);

public sealed class NoConcretadaRequest
{
    [Required, StringLength(300, MinimumLength = 3)] public string Motivo { get; init; } = "";
}

public sealed class CalificarRequest
{
    [Range(1, 5)] public int Estrellas { get; init; }
    [MaxLength(300)] public string? Comentario { get; init; }
}

/// <summary>Reseña pública. Comentario es null si la moderación lo ocultó (las estrellas siguen contando).</summary>
public sealed record CalificacionDto(Guid Id, PerfilPublicoDto Autor, int Estrellas, string? Comentario, DateTime FechaUtc);

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
