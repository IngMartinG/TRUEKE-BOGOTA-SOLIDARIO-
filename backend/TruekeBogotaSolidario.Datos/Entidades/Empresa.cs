using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Pendiente → Aprobada | Rechazada. Una aprobada puede quedar Retirada (moderación). Rechazada o Retirada pueden volver a solicitar.</summary>
public enum EstadoEmpresa { Pendiente = 1, Aprobada = 2, Rechazada = 3, Retirada = 4 }

/// <summary>Admin gestiona miembros, invitaciones y campañas; Miembro liga sus publicaciones a las campañas.</summary>
public enum RolMiembroEmpresa { Admin = 1, Miembro = 2 }

/// <summary>
/// Empresa (persona jurídica) que mide su impacto con campañas e informes. No es un rol de seguridad: sus miembros son cuentas
/// normales. Se contrata por cotización (sin cobro en la plataforma) y la verifica un SuperUsuario con el certificado de la
/// Cámara de Comercio, igual que una fundación: el certificado vive en el bucket privado y se borra al decidir.
/// </summary>
public class Empresa
{
    private Empresa() { RazonSocial = ""; Nit = ""; } // EF Core

    public Empresa(Guid solicitadaPorId, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        SolicitadaPorId = solicitadaPorId;
        RazonSocial = ""; Nit = "";
        FechaSolicitudUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public string RazonSocial { get; private set; }
    /// <summary>NIT con dígito de verificación ("900123456-7").</summary>
    public string Nit { get; private set; }
    public EstadoEmpresa Estado { get; private set; }
    /// <summary>Certificado de existencia (bucket privado). Solo existe mientras la solicitud está pendiente.</summary>
    public string? CertificadoUrl { get; private set; }
    public string? MotivoDecision { get; private set; }
    /// <summary>Quien la registró (queda como primer Admin).</summary>
    public Guid SolicitadaPorId { get; private set; }
    public DateTime FechaSolicitudUtc { get; private set; }
    public DateTime? FechaDecisionUtc { get; private set; }

    public bool EstaAprobada => Estado == EstadoEmpresa.Aprobada;

    /// <summary>Persona jurídica: su NIT empieza por 8 o 9. La prueba real es el certificado que revisa el SuperUsuario.</summary>
    public static string NormalizarNitJuridica(string? nit)
    {
        var normalizado = DocumentosFiscales.NormalizarNit(nit);
        if (normalizado[0] is not ('8' or '9') || normalizado.Length < 11)
            throw new ReglaDeNegocioException("Escribe el NIT de la empresa (persona jurídica: empieza por 8 o 9 y tiene 9 dígitos más el de verificación).");
        return normalizado;
    }

    public void Solicitar(string razonSocial, string nit, string certificadoUrl, DateTime ahoraUtc)
    {
        if (Estado == EstadoEmpresa.Pendiente && CertificadoUrl is not null) throw new ReglaDeNegocioException("Ya hay una solicitud en revisión.");
        if (Estado == EstadoEmpresa.Aprobada) throw new ReglaDeNegocioException("La empresa ya está verificada.");
        if (!UrlsSeguras.EsValida(certificadoUrl)) throw new ReglaDeNegocioException("El certificado debe ser una URL https válida.");
        var nombre = FormatosTexto.Compactar(razonSocial);
        if (nombre.Length is < 3 or > 150) throw new ReglaDeNegocioException("La razón social debe tener entre 3 y 150 caracteres.");
        if (!FormatosTexto.EsNombreComercial(nombre))
            throw new ReglaDeNegocioException("La razón social solo puede tener letras, números, espacios y & . , - ' ( ).");
        RazonSocial = nombre;
        Nit = NormalizarNitJuridica(nit);
        CertificadoUrl = certificadoUrl;
        Estado = EstadoEmpresa.Pendiente;
        MotivoDecision = null;
        FechaSolicitudUtc = ahoraUtc;
        FechaDecisionUtc = null;
    }

    public void Aprobar(DateTime ahoraUtc)
    {
        ExigirPendiente();
        Estado = EstadoEmpresa.Aprobada;
        MotivoDecision = null;
        CertificadoUrl = null; // minimización: decidido, el certificado ya no se conserva
        FechaDecisionUtc = ahoraUtc;
    }

    public void Rechazar(string motivo, DateTime ahoraUtc)
    {
        ExigirPendiente();
        Estado = EstadoEmpresa.Rechazada;
        MotivoDecision = ExigirMotivo(motivo);
        CertificadoUrl = null;
        FechaDecisionUtc = ahoraUtc;
    }

    /// <summary>Moderación: deja de estar verificada (p. ej. se liquidó o usó la plataforma para engañar). Sus informes se conservan.</summary>
    public void Retirar(string motivo, DateTime ahoraUtc)
    {
        if (Estado != EstadoEmpresa.Aprobada) throw new ReglaDeNegocioException("Esta empresa no está verificada.");
        Estado = EstadoEmpresa.Retirada;
        MotivoDecision = ExigirMotivo(motivo);
        FechaDecisionUtc = ahoraUtc;
    }

    private void ExigirPendiente()
    {
        if (Estado != EstadoEmpresa.Pendiente || CertificadoUrl is null)
            throw new ReglaDeNegocioException("No hay una solicitud de empresa pendiente.");
    }

    private static string ExigirMotivo(string motivo)
    {
        motivo = (motivo ?? "").Trim();
        if (motivo.Length == 0) throw new ReglaDeNegocioException("Debes indicar el motivo.");
        return motivo.Length > 300 ? motivo[..300] : motivo;
    }
}

/// <summary>
/// Una cuenta en una empresa, o una invitación aún sin usar (UsuarioId null). La invitación es un enlace con un código de un
/// solo uso que el Admin comparte por su cuenta: la plataforma nunca busca personas por correo ni revela si una cuenta existe.
/// Una cuenta pertenece como mucho a una empresa.
/// </summary>
public class MiembroEmpresa
{
    public static readonly TimeSpan VigenciaInvitacion = TimeSpan.FromDays(7);
    public const int MaxMiembros = 50;

    private MiembroEmpresa() { } // EF Core

    private MiembroEmpresa(Guid empresaId, RolMiembroEmpresa rol, Guid invitadoPorId, DateTime ahoraUtc)
    {
        if (!Enum.IsDefined(rol)) throw new ReglaDeNegocioException("Rol no válido.");
        Id = Guid.NewGuid();
        EmpresaId = empresaId;
        Rol = rol;
        InvitadoPorId = invitadoPorId;
        FechaInvitacionUtc = ahoraUtc;
    }

    /// <summary>Quien registra la empresa entra de una vez como Admin.</summary>
    public static MiembroEmpresa Fundador(Guid empresaId, Guid usuarioId, DateTime ahoraUtc)
        => new(empresaId, RolMiembroEmpresa.Admin, usuarioId, ahoraUtc) { UsuarioId = usuarioId, FechaIngresoUtc = ahoraUtc };

    /// <param name="codigoHash">SHA-256 del código del enlace (el código en claro solo lo ve una vez quien invita).</param>
    public static MiembroEmpresa Invitacion(Guid empresaId, RolMiembroEmpresa rol, string codigoHash, Guid invitadoPorId, DateTime ahoraUtc)
        => new(empresaId, rol, invitadoPorId, ahoraUtc) { CodigoHash = codigoHash, ExpiraUtc = ahoraUtc + VigenciaInvitacion };

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    /// <summary>null mientras sea una invitación sin aceptar.</summary>
    public Guid? UsuarioId { get; private set; }
    public RolMiembroEmpresa Rol { get; private set; }
    public string? CodigoHash { get; private set; }
    public DateTime? ExpiraUtc { get; private set; }
    public Guid InvitadoPorId { get; private set; }
    public DateTime FechaInvitacionUtc { get; private set; }
    public DateTime? FechaIngresoUtc { get; private set; }

    public bool EsActivo => UsuarioId is not null;
    public bool EsAdmin => EsActivo && Rol == RolMiembroEmpresa.Admin;
    public bool InvitacionVigente(DateTime ahoraUtc) => !EsActivo && ExpiraUtc > ahoraUtc;

    public void Aceptar(Guid usuarioId, DateTime ahoraUtc)
    {
        if (EsActivo || !InvitacionVigente(ahoraUtc)) throw new ReglaDeNegocioException("La invitación venció o ya se usó. Pide una nueva.");
        UsuarioId = usuarioId;
        CodigoHash = null;
        ExpiraUtc = null;
        FechaIngresoUtc = ahoraUtc;
    }

    public void CambiarRol(RolMiembroEmpresa rol)
    {
        if (!Enum.IsDefined(rol)) throw new ReglaDeNegocioException("Rol no válido.");
        Rol = rol;
    }
}

/// <summary>
/// Campaña de una empresa (p. ej. "Donatón de uniformes 2026"). Sus miembros ligan publicaciones mientras está activa y cada
/// objeto entregado guarda la campaña en su registro de impacto: los informes salen de esos registros.
/// </summary>
public class Campana
{
    public const int MaxDias = 366;
    public const int LongitudNombre = 100;

    private Campana() { Nombre = ""; } // EF Core

    public Campana(Guid empresaId, string nombre, DateOnly inicio, DateOnly fin, Guid creadaPorId, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        EmpresaId = empresaId;
        Nombre = "";
        CreadaPorId = creadaPorId;
        CreadaUtc = ahoraUtc;
        Editar(nombre, inicio, fin, Usuario.HoyEnColombia(ahoraUtc));
    }

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    public string Nombre { get; private set; }
    /// <summary>Días calendario de Colombia, ambos incluidos.</summary>
    public DateOnly Inicio { get; private set; }
    public DateOnly Fin { get; private set; }
    public Guid CreadaPorId { get; private set; }
    public DateTime CreadaUtc { get; private set; }

    public bool EstaActiva(DateOnly hoy) => Inicio <= hoy && hoy <= Fin;

    public void Editar(string nombre, DateOnly inicio, DateOnly fin, DateOnly hoy)
    {
        nombre = FormatosTexto.Compactar(nombre);
        if (nombre.Length is < 3 or > LongitudNombre) throw new ReglaDeNegocioException($"El nombre de la campaña debe tener entre 3 y {LongitudNombre} caracteres.");
        if (!TextoSeguro.EsSeguro(nombre)) throw new ReglaDeNegocioException(TextoSeguro.Mensaje);
        if (fin < inicio) throw new ReglaDeNegocioException("La campaña no puede terminar antes de empezar.");
        if (fin.DayNumber - inicio.DayNumber + 1 > MaxDias) throw new ReglaDeNegocioException($"Una campaña dura como mucho {MaxDias} días.");
        if (fin < hoy && fin != Fin) throw new ReglaDeNegocioException("La fecha de fin no puede quedar en el pasado.");
        Nombre = nombre;
        Inicio = inicio;
        Fin = fin;
    }
}

/// <summary>
/// Informe de impacto emitido por un Administrador. Guarda una foto inmutable (JSON) de los registros que tomó, con el factor y
/// la versión de la metodología de cada uno, y su huella SHA-256: el PDF y el CSV se generan siempre desde esa foto, así que no
/// cambian aunque después cambien los factores, las campañas o las publicaciones.
/// </summary>
public class InformeImpacto
{
    private InformeImpacto() { Contenido = ""; Huella = ""; Versiones = ""; } // EF Core

    public InformeImpacto(Guid empresaId, Guid? campanaId, DateOnly desde, DateOnly hasta, Guid emitidoPorId, DateTime ahoraUtc,
        string versiones, string contenido, string huella)
    {
        Id = Guid.NewGuid();
        EmpresaId = empresaId;
        CampanaId = campanaId;
        Desde = desde;
        Hasta = hasta;
        EmitidoPorId = emitidoPorId;
        EmitidoUtc = ahoraUtc;
        Versiones = versiones;
        Contenido = contenido;
        Huella = huella;
    }

    public Guid Id { get; private set; }
    public Guid EmpresaId { get; private set; }
    /// <summary>null = todas las campañas de la empresa.</summary>
    public Guid? CampanaId { get; private set; }
    public DateOnly Desde { get; private set; }
    public DateOnly Hasta { get; private set; }
    public Guid EmitidoPorId { get; private set; }
    public DateTime EmitidoUtc { get; private set; }
    /// <summary>Versiones de la metodología presentes en los registros ("2.0.0" o "2.0.0, 2.1.0").</summary>
    public string Versiones { get; private set; }
    /// <summary>Foto de los datos (JSON). Nunca se reescribe.</summary>
    public string Contenido { get; private set; }
    /// <summary>SHA-256 (hex) del contenido: va impresa en el PDF para comprobar que no se alteró.</summary>
    public string Huella { get; private set; }
}

/// <summary>
/// Foto opcional que una de las partes adjunta al confirmar la entrega de una solicitud (una por persona). Queda en el bucket
/// privado del chat, limpia de metadatos: solo la ven las dos partes y la administración, y el informe solo dice si existe.
/// </summary>
public class EvidenciaEntrega
{
    private EvidenciaEntrega() { FotoUrl = ""; } // EF Core

    public EvidenciaEntrega(Guid solicitudId, Guid usuarioId, string fotoUrl, DateTime ahoraUtc)
    {
        if (!UrlsSeguras.EsValida(fotoUrl)) throw new ReglaDeNegocioException("La foto debe ser una URL https válida.");
        Id = Guid.NewGuid();
        SolicitudId = solicitudId;
        UsuarioId = usuarioId;
        FotoUrl = fotoUrl;
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid SolicitudId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string FotoUrl { get; private set; }
    public DateTime FechaUtc { get; private set; }
}
