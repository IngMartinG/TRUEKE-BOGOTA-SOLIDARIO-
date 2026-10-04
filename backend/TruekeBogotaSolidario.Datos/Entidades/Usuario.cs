using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public class Usuario
{
    private Usuario() { NombreCompleto = ""; Localidad = ""; Correo = ""; CorreoCanonico = ""; ClaveHash = ""; MunicipioCodigo = Divipola.CodigoBogota; } // EF Core

    public Usuario(string nombreCompleto, string localidad, string correo, string claveEnClaro, string? municipioCodigo = null)
    {
        Id = Guid.NewGuid();
        (NombreCompleto, Localidad) = ValidarPerfil(nombreCompleto, localidad);
        MunicipioCodigo = Divipola.Exigir(municipioCodigo ?? Divipola.CodigoBogota).Codigo;
        Correo = NormalizarCorreo(correo);
        if (Correo.Length is 0 or > 160 || !Correo.Contains('@')) throw new ReglaDeNegocioException("El correo no es válido.");
        CorreoCanonico = CanonizarCorreo(Correo);
        ClaveHash = "";
        EstablecerClave(claveEnClaro);
        Rol = RolUsuarioEnum.Cliente;
        TipoCuenta = TipoCuenta.Individual;
        EstadoVerificacion = EstadoVerificacion.NoVerificado;
        FechaRegistro = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string NombreCompleto { get; private set; }
    /// <summary>Barrio, localidad o sector (texto libre). El municipio va en <see cref="MunicipioCodigo"/>.</summary>
    public string Localidad { get; private set; }
    /// <summary>Código DIVIPOLA del municipio de residencia (Bogotá = 11001).</summary>
    public string MunicipioCodigo { get; private set; }
    public string Correo { get; private set; }
    /// <summary>
    /// Forma canónica del correo (sin alias "+algo" y, en Gmail, sin puntos). Es ÚNICA: impide abrir varias cuentas
    /// con el mismo buzón (yo+1@gmail.com, y.o@gmail.com…) para farmear Eco-Puntos o reputación.
    /// </summary>
    public string CorreoCanonico { get; private set; }
    public string ClaveHash { get; private set; }
    public DateTime FechaRegistro { get; private set; }

    /// <summary>Los Eco-Puntos de bienvenida se dan una sola vez, al demostrar que el correo es propio.</summary>
    public bool BonoBienvenidaOtorgado { get; private set; }

    // Perfil de empresa (solo se muestra con el plan Empresa vigente)
    public string? NombreComercial { get; private set; }
    public string? Nit { get; private set; }

    // Datos para la factura electrónica (opcionales: sin ellos se factura a "consumidor final")
    public TipoDocumentoFiscal? FacturacionTipoDocumento { get; private set; }
    public string? FacturacionDocumento { get; private set; }
    public string? FacturacionNombre { get; private set; }
    public string? FacturacionCorreo { get; private set; }
    public string? FacturacionDireccion { get; private set; }
    public string? FacturacionMunicipioCodigo { get; private set; }

    /// <summary>Vencimiento del plan para el que ya se envió el recordatorio (evita repetirlo).</summary>
    public DateTime? RecordatorioVencimientoPara { get; private set; }

    /// <summary>Se incrementa al cambiar rol o contraseña: invalida todos los JWT emitidos antes (revocación de sesiones).</summary>
    public int VersionSeguridad { get; private set; }

    // Seguridad de login
    public int IntentosFallidosLogin { get; private set; }
    public DateTime? BloqueadoHasta { get; private set; }

    // Economía
    public int SaldoEcoPuntos { get; private set; }
    public decimal Reputacion { get; private set; }
    public int TotalTruekesCompletados { get; private set; }
    public int TotalComprasRealizadas { get; private set; }
    public int TotalDonacionesRealizadas { get; private set; }

    // Rol y plan
    public RolUsuarioEnum Rol { get; private set; }
    public TipoCuenta TipoCuenta { get; private set; }
    public DateTime? FechaVencimientoSuscripcion { get; private set; }
    public int DestacadosGratisRestantes { get; private set; }

    // Verificación de identidad
    public EstadoVerificacion EstadoVerificacion { get; private set; }
    public string? DocumentoVerificacionUrl { get; private set; }
    /// <summary>Foto de perfil pública (copia limpia, sin GPS ni metadatos). null = se muestran las iniciales.</summary>
    public string? FotoUrl { get; private set; }

    // Preferencias de avisos por correo. Los avisos obligatorios (seguridad, pagos, PQR, moderación) no dependen de esto.
    public bool AvisosCorreoIntercambios { get; private set; } = true;
    public bool AvisosCorreoMensajes { get; private set; } = true;
    public bool AvisosCorreoPlanes { get; private set; } = true;
    /// <summary>Novedades y consejos: comunicaciones no transaccionales, solo con consentimiento expreso (Ley 1581).</summary>
    public bool AceptaNovedades { get; private set; }

    public void ActualizarPreferenciasAvisos(bool intercambios, bool mensajes, bool planes, bool novedades)
    {
        AvisosCorreoIntercambios = intercambios;
        AvisosCorreoMensajes = mensajes;
        AvisosCorreoPlanes = planes;
        AceptaNovedades = novedades;
    }
    public string? MotivoRechazoVerificacion { get; private set; }

    // Identidad y cumplimiento (Ley 1581 de 2012)
    public bool CorreoVerificado { get; private set; }
    public DateTime? FechaVerificacionCorreo { get; private set; }
    /// <summary>Identificador estable de la cuenta de Google ("sub"), si se vinculó.</summary>
    public string? GoogleSub { get; private set; }
    public string? PoliticaDatosVersion { get; private set; }
    public DateTime? FechaAceptacionPolitica { get; private set; }
    public bool EstaEliminado { get; private set; }
    public DateTime? FechaEliminacion { get; private set; }

    // Moderación de cuentas
    public bool EstaSuspendido { get; private set; }
    /// <summary>null con EstaSuspendido = suspensión indefinida.</summary>
    public DateTime? SuspendidoHasta { get; private set; }
    public string? MotivoSuspension { get; private set; }

    // Calificaciones recibidas (contadores para no agregar en cada consulta)
    public int CalificacionesTotal { get; private set; }
    public int CalificacionesSuma { get; private set; }

    // Verificación en dos pasos (TOTP). El secreto se guarda CIFRADO (AES-GCM) y los códigos de recuperación como hashes.
    public bool DosFactoresActivo { get; private set; }
    public string? SecretoDosFactoresCifrado { get; private set; }
    /// <summary>Último paso de 30 s aceptado: impide reutilizar el mismo código.</summary>
    public long UltimoPasoDosFactores { get; private set; }
    /// <summary>Hashes SHA-256 de los códigos de recuperación no usados, separados por ";".</summary>
    public string? CodigosRecuperacionHash { get; private set; }

    /// <summary>Token de concurrencia optimista (solo SQL Server).</summary>
    public byte[]? RowVersion { get; private set; }

    public bool EsVerificado => EstadoVerificacion == EstadoVerificacion.Aprobada;

    // ---------------- Perfil ----------------
    public static string NormalizarCorreo(string correo) => (correo ?? "").Trim().ToLowerInvariant();

    /// <summary>"Ana.Perez+promo@GoogleMail.com" → "anaperez@gmail.com". Para otros dominios solo se quita el alias "+…".</summary>
    public static string CanonizarCorreo(string correo)
    {
        var c = NormalizarCorreo(correo);
        var arroba = c.LastIndexOf('@');
        if (arroba <= 0) return c;
        var local = c[..arroba];
        var dominio = c[(arroba + 1)..];
        if (dominio == "googlemail.com") dominio = "gmail.com";
        var mas = local.IndexOf('+');
        if (mas > 0) local = local[..mas];
        if (dominio == "gmail.com") local = local.Replace(".", "", StringComparison.Ordinal);
        return local.Length == 0 ? c : $"{local}@{dominio}";
    }

    private static (string nombre, string localidad) ValidarPerfil(string nombreCompleto, string localidad)
    {
        var n = (nombreCompleto ?? "").Trim();
        var l = (localidad ?? "").Trim();
        if (n.Length is < 3 or > 120) throw new ReglaDeNegocioException("El nombre debe tener entre 3 y 120 caracteres.");
        if (l.Length is < 2 or > 60) throw new ReglaDeNegocioException("La localidad debe tener entre 2 y 60 caracteres.");
        if (n.Any(char.IsControl) || l.Any(char.IsControl)) throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        return (n, l);
    }

    public void ActualizarPerfil(string nombreCompleto, string localidad, string? municipioCodigo = null)
    {
        (NombreCompleto, Localidad) = ValidarPerfil(nombreCompleto, localidad);
        if (municipioCodigo is not null) MunicipioCodigo = Divipola.Exigir(municipioCodigo).Codigo;
    }

    /// <summary>La URL ya debe venir validada (archivo propio y limpio). Devuelve la foto anterior para borrarla.</summary>
    public string? CambiarFoto(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500) throw new ReglaDeNegocioException("La foto no es válida.");
        var anterior = FotoUrl;
        FotoUrl = url;
        return anterior;
    }

    public string? QuitarFoto()
    {
        var anterior = FotoUrl;
        FotoUrl = null;
        return anterior;
    }

    /// <summary>Nombre comercial y NIT visibles en el perfil público mientras el plan Empresa esté vigente.</summary>
    public void ActualizarPerfilEmpresa(string nombreComercial, string nit, DateTime ahoraUtc)
    {
        if (!EmpresaVigente(ahoraUtc)) throw new ReglaDeNegocioException("El perfil de empresa está disponible con el plan Empresa vigente.");
        var nombre = (nombreComercial ?? "").Trim();
        if (nombre.Length is < 2 or > 120 || nombre.Any(char.IsControl)) throw new ReglaDeNegocioException("El nombre comercial debe tener entre 2 y 120 caracteres.");
        NombreComercial = nombre;
        Nit = DocumentosFiscales.NormalizarNit(nit);
    }

    // ---------------- Facturación electrónica ----------------
    public bool TieneDatosFacturacion => FacturacionTipoDocumento.HasValue;

    public void ActualizarDatosFacturacion(TipoDocumentoFiscal tipo, string documento, string nombre, string correo, string? direccion, string? municipioCodigo)
    {
        if (!Enum.IsDefined(tipo)) throw new ReglaDeNegocioException("Tipo de documento no válido.");
        var doc = DocumentosFiscales.Normalizar(tipo, documento);
        var n = (nombre ?? "").Trim();
        if (n.Length is < 3 or > 150 || n.Any(char.IsControl)) throw new ReglaDeNegocioException("El nombre o razón social debe tener entre 3 y 150 caracteres.");
        var c = NormalizarCorreo(correo);
        if (c.Length is < 5 or > 160 || !c.Contains('@')) throw new ReglaDeNegocioException("El correo para la factura no es válido.");
        var dir = string.IsNullOrWhiteSpace(direccion) ? null : direccion.Trim();
        if (dir is { Length: > 150 } || dir?.Any(char.IsControl) == true) throw new ReglaDeNegocioException("La dirección admite como máximo 150 caracteres.");
        var mpio = string.IsNullOrWhiteSpace(municipioCodigo) ? null : Divipola.Exigir(municipioCodigo).Codigo;
        FacturacionTipoDocumento = tipo;
        FacturacionDocumento = doc;
        FacturacionNombre = n;
        FacturacionCorreo = c;
        FacturacionDireccion = dir;
        FacturacionMunicipioCodigo = mpio;
    }

    public void BorrarDatosFacturacion()
    {
        FacturacionTipoDocumento = null;
        FacturacionDocumento = null;
        FacturacionNombre = null;
        FacturacionCorreo = null;
        FacturacionDireccion = null;
        FacturacionMunicipioCodigo = null;
    }

    // ---------------- Contraseña y bloqueo ----------------
    public void EstablecerClave(string claveEnClaro)
    {
        if (claveEnClaro is null || claveEnClaro.Length is < 8 or > 128
            || !claveEnClaro.Any(char.IsUpper) || !claveEnClaro.Any(char.IsLower) || !claveEnClaro.Any(char.IsDigit))
            throw new ReglaDeNegocioException("La contraseña debe tener entre 8 y 128 caracteres e incluir mayúscula, minúscula y número.");
        ClaveHash = PasswordHasher.Hash(claveEnClaro);
        VersionSeguridad++;
    }

    public void InvalidarSesiones() => VersionSeguridad++;

    public bool VerificarClave(string claveEnClaro) => TieneClave && PasswordHasher.Verificar(claveEnClaro, ClaveHash);

    /// <summary>Falso en cuentas creadas solo con Google (o eliminadas): no se puede iniciar sesión con contraseña.</summary>
    public bool TieneClave => ClaveHash.Length > 0;

    public bool EstaBloqueado(DateTime ahoraUtc) => BloqueadoHasta.HasValue && BloqueadoHasta.Value > ahoraUtc;

    public void RegistrarLoginFallido(int maxIntentos, TimeSpan duracionBloqueo, DateTime? ahoraUtc = null)
    {
        IntentosFallidosLogin++;
        if (IntentosFallidosLogin >= maxIntentos)
        {
            BloqueadoHasta = (ahoraUtc ?? DateTime.UtcNow) + duracionBloqueo;
            IntentosFallidosLogin = 0;
        }
    }

    public void RegistrarLoginExitoso()
    {
        IntentosFallidosLogin = 0;
        BloqueadoHasta = null;
    }

    // ---------------- Correo, Google y datos personales ----------------
    /// <summary>Al demostrar que el correo es propio se entregan (una sola vez) los Eco-Puntos de bienvenida.</summary>
    public void MarcarCorreoVerificado(DateTime ahoraUtc)
    {
        if (CorreoVerificado) return;
        CorreoVerificado = true;
        FechaVerificacionCorreo = ahoraUtc;
        if (!BonoBienvenidaOtorgado && !EstaEliminado)
        {
            AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosBienvenida);
            BonoBienvenidaOtorgado = true;
        }
    }

    public void AceptarPoliticaDatos(string version, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 20) throw new ReglaDeNegocioException("Versión de política no válida.");
        PoliticaDatosVersion = version;
        FechaAceptacionPolitica = ahoraUtc;
    }

    /// <summary>Cuenta creada con Google: sin contraseña (no se puede entrar con clave hasta que el usuario cree una) y correo ya verificado.</summary>
    public static Usuario CrearDesdeGoogle(string nombre, string correo, string googleSub, DateTime ahoraUtc)
    {
        var nombreValido = (nombre ?? "").Trim();
        if (nombreValido.Length is < 3 or > 120 || nombreValido.Any(char.IsControl)) nombreValido = "Usuario Trueke";
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = nombreValido,
            Localidad = "Sin especificar",
            MunicipioCodigo = Divipola.CodigoBogota,
            Correo = NormalizarCorreo(correo),
            CorreoCanonico = CanonizarCorreo(correo),
            ClaveHash = "",
            Rol = RolUsuarioEnum.Cliente,
            TipoCuenta = TipoCuenta.Individual,
            EstadoVerificacion = EstadoVerificacion.NoVerificado,
            FechaRegistro = ahoraUtc
        };
        if (u.Correo.Length is 0 or > 160 || !u.Correo.Contains('@')) throw new ReglaDeNegocioException("El correo no es válido.");
        u.VincularGoogle(googleSub);
        u.MarcarCorreoVerificado(ahoraUtc);
        return u;
    }

    /// <summary>
    /// Elimina la contraseña y revoca los tokens. Se usa al vincular Google a una cuenta cuyo correo NUNCA se verificó:
    /// quien la creó con esa clave no demostró ser el dueño del correo (evita el "secuestro previo" de cuentas).
    /// </summary>
    public void QuitarClave()
    {
        ClaveHash = "";
        VersionSeguridad++;
    }

    public void VincularGoogle(string googleSub)
    {
        if (string.IsNullOrWhiteSpace(googleSub) || googleSub.Length > 64) throw new ReglaDeNegocioException("Cuenta de Google no válida.");
        if (GoogleSub is not null && GoogleSub != googleSub) throw new ReglaDeNegocioException("La cuenta ya está vinculada a otro usuario de Google.");
        GoogleSub = googleSub;
    }

    /// <summary>
    /// Derecho de supresión: borra los datos que identifican a la persona y deja el registro solo para la integridad
    /// de pagos y transacciones (obligación contable). La cuenta ya no puede iniciar sesión por ningún medio.
    /// </summary>
    public void Anonimizar(DateTime ahoraUtc)
    {
        if (EstaEliminado) throw new ReglaDeNegocioException("La cuenta ya fue eliminada.");
        NombreCompleto = "Usuario eliminado";
        Localidad = "Sin especificar";
        Correo = $"eliminado-{Id:N}@trueke.invalid";
        CorreoCanonico = Correo;
        ClaveHash = "";
        GoogleSub = null;
        NombreComercial = null;
        Nit = null;
        BorrarDatosFacturacion();
        DocumentoVerificacionUrl = null;
        MotivoRechazoVerificacion = null;
        FotoUrl = null;
        SaldoEcoPuntos = 0;
        DestacadosGratisRestantes = 0;
        IntentosFallidosLogin = 0;
        BloqueadoHasta = null;
        CorreoVerificado = false;
        DosFactoresActivo = false;
        SecretoDosFactoresCifrado = null;
        CodigosRecuperacionHash = null;
        EstaEliminado = true;
        FechaEliminacion = ahoraUtc;
        VersionSeguridad++; // todos los tokens de acceso dejan de valer
    }

    // ---------------- Suspensión (moderación) ----------------
    public bool SuspensionVigente(DateTime ahoraUtc) => EstaSuspendido && (SuspendidoHasta is null || SuspendidoHasta > ahoraUtc);

    public void Suspender(string motivo, DateTime? hastaUtc, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Indica el motivo de la suspensión.");
        if (hastaUtc.HasValue && hastaUtc <= ahoraUtc) throw new ReglaDeNegocioException("La fecha de fin debe ser futura.");
        if (EstaEliminado) throw new ReglaDeNegocioException("La cuenta fue eliminada.");
        EstaSuspendido = true;
        SuspendidoHasta = hastaUtc;
        MotivoSuspension = motivo.Trim().Length > 300 ? motivo.Trim()[..300] : motivo.Trim();
        VersionSeguridad++; // cierra todas sus sesiones de inmediato
    }

    public void Reactivar()
    {
        if (!EstaSuspendido) throw new ReglaDeNegocioException("La cuenta no está suspendida.");
        EstaSuspendido = false;
        SuspendidoHasta = null;
        MotivoSuspension = null;
    }

    // ---------------- Calificaciones ----------------
    public void RegistrarCalificacion(int estrellas)
    {
        if (estrellas is < 1 or > 5) throw new ReglaDeNegocioException("La calificación debe estar entre 1 y 5 estrellas.");
        CalificacionesTotal++;
        CalificacionesSuma += estrellas;
    }

    public decimal? CalificacionPromedio => CalificacionesTotal == 0 ? null : Math.Round((decimal)CalificacionesSuma / CalificacionesTotal, 1);

    // ---------------- Verificación en dos pasos ----------------
    /// <summary>Guarda un secreto nuevo SIN activarlo (se activa al confirmar un código).</summary>
    public void PrepararDosFactores(string secretoCifrado)
    {
        if (DosFactoresActivo) throw new ReglaDeNegocioException("La verificación en dos pasos ya está activa.");
        SecretoDosFactoresCifrado = secretoCifrado;
    }

    public void ActivarDosFactores(IEnumerable<string> hashesRecuperacion, long paso)
    {
        if (DosFactoresActivo) throw new ReglaDeNegocioException("La verificación en dos pasos ya está activa.");
        if (SecretoDosFactoresCifrado is null) throw new ReglaDeNegocioException("Primero genera el código QR.");
        DosFactoresActivo = true;
        UltimoPasoDosFactores = paso;
        CodigosRecuperacionHash = string.Join(';', hashesRecuperacion);
        VersionSeguridad++; // las sesiones previas (sin 2FA) dejan de valer
    }

    public void DesactivarDosFactores()
    {
        DosFactoresActivo = false;
        SecretoDosFactoresCifrado = null;
        CodigosRecuperacionHash = null;
        UltimoPasoDosFactores = 0;
        VersionSeguridad++;
    }

    /// <summary>Acepta un paso TOTP solo si es posterior al último usado (anti-repetición).</summary>
    public bool RegistrarPasoDosFactores(long paso)
    {
        if (paso <= UltimoPasoDosFactores) return false;
        UltimoPasoDosFactores = paso;
        return true;
    }

    /// <summary>Consume un código de recuperación (por hash). Cada uno sirve una sola vez.</summary>
    public bool UsarCodigoRecuperacion(string hash)
    {
        if (string.IsNullOrEmpty(CodigosRecuperacionHash)) return false;
        var lista = CodigosRecuperacionHash.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (!lista.Remove(hash)) return false;
        CodigosRecuperacionHash = string.Join(';', lista);
        return true;
    }

    /// <summary>Invalida todos los códigos de recuperación anteriores y guarda los nuevos (sin cerrar sesiones).</summary>
    public void ReemplazarCodigosRecuperacion(IEnumerable<string> hashes)
    {
        if (!DosFactoresActivo) throw new ReglaDeNegocioException("La verificación en dos pasos no está activa.");
        CodigosRecuperacionHash = string.Join(';', hashes);
    }

    public int CodigosRecuperacionRestantes => string.IsNullOrEmpty(CodigosRecuperacionHash)
        ? 0 : CodigosRecuperacionHash.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;

    // ---------------- Roles ----------------
    public void CambiarRol(RolUsuarioEnum nuevoRol)
    {
        if (nuevoRol == RolUsuarioEnum.Invitado || !Enum.IsDefined(nuevoRol))
            throw new ReglaDeNegocioException("Rol no válido: 'Invitado' representa a un visitante sin cuenta y no puede asignarse.");
        if (Rol == nuevoRol) return;
        Rol = nuevoRol;
        VersionSeguridad++; // los tokens con el rol anterior dejan de valer
    }

    // ---------------- Eco-Puntos ----------------
    public void AcreditarEcoPuntos(int puntos)
    {
        if (puntos <= 0) throw new ReglaDeNegocioException("Los puntos a acreditar deben ser positivos.");
        SaldoEcoPuntos = checked(SaldoEcoPuntos + puntos);
    }

    public void DebitarEcoPuntos(int puntos)
    {
        if (puntos <= 0) throw new ReglaDeNegocioException("Los puntos a debitar deben ser positivos.");
        if (puntos > SaldoEcoPuntos) throw new ReglaDeNegocioException("Saldo de Eco-Puntos insuficiente.");
        SaldoEcoPuntos -= puntos;
    }

    /// <summary>El contador siempre sube; los puntos y la reputación solo si <paramref name="otorgarPuntos"/> (tope anti-farmeo).</summary>
    public void RegistrarTransaccionCompletada(ModoTransaccion modo, bool otorgarPuntos = true)
    {
        switch (modo)
        {
            case ModoTransaccion.Trueke: TotalTruekesCompletados++; break;
            case ModoTransaccion.Compra: TotalComprasRealizadas++; break;
            case ModoTransaccion.Donacion: TotalDonacionesRealizadas++; break;
            default: throw new ReglaDeNegocioException("Modo de transacción no válido.");
        }
        if (!otorgarPuntos) return;
        AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosPorModo(modo));
        Reputacion = Math.Min(PoliticaEcoPuntos.ReputacionMaxima, Reputacion + PoliticaEcoPuntos.ReputacionPorModo(modo));
    }

    // ---------------- Planes ----------------
    public bool PremiumVigente(DateTime ahoraUtc)
        => TipoCuenta == TipoCuenta.Premium && FechaVencimientoSuscripcion.HasValue && FechaVencimientoSuscripcion.Value > ahoraUtc;

    public bool EmpresaVigente(DateTime ahoraUtc)
        => TipoCuenta == TipoCuenta.Empresa && FechaVencimientoSuscripcion.HasValue && FechaVencimientoSuscripcion.Value > ahoraUtc;

    /// <summary>Plan que realmente aplica hoy (un plan vencido vuelve a Individual).</summary>
    public TipoCuenta PlanEfectivo(DateTime ahoraUtc)
        => PremiumVigente(ahoraUtc) ? TipoCuenta.Premium : EmpresaVigente(ahoraUtc) ? TipoCuenta.Empresa : TipoCuenta.Individual;

    public bool PlanPagoVigente(DateTime ahoraUtc) => PlanEfectivo(ahoraUtc) != TipoCuenta.Individual;

    public void MarcarRecordatorioVencimiento(DateTime vencimiento) => RecordatorioVencimientoPara = vencimiento;

    /// <summary>
    /// Reembolso de un mes de plan (retracto o reversión): se descuentan los días pagados. Si el plan queda vencido,
    /// se pierden los destacados gratis restantes.
    /// </summary>
    public void RevertirMesDePlan(TipoCuenta plan, DateTime ahoraUtc)
    {
        if (TipoCuenta != plan || FechaVencimientoSuscripcion is null) return;
        FechaVencimientoSuscripcion = FechaVencimientoSuscripcion.Value.AddDays(-PoliticaEcoPuntos.DuracionSuscripcionDias);
        if (FechaVencimientoSuscripcion <= ahoraUtc)
        {
            FechaVencimientoSuscripcion = ahoraUtc;
            DestacadosGratisRestantes = 0;
        }
    }

    /// <summary>Reembolso de una recarga: se retiran los puntos acreditados que aún queden (el saldo nunca queda negativo).</summary>
    public int RetirarEcoPuntosHasta(int puntos)
    {
        var retirados = Math.Clamp(puntos, 0, SaldoEcoPuntos);
        SaldoEcoPuntos -= retirados;
        return retirados;
    }

    /// <summary>Reembolso de la verificación antes de que un moderador la resuelva.</summary>
    public void CancelarVerificacionPendiente()
    {
        if (EstadoVerificacion != EstadoVerificacion.Pendiente) return;
        EstadoVerificacion = EstadoVerificacion.NoVerificado;
        DocumentoVerificacionUrl = null;
    }

    public void ValidarPuedeSuscribirse(TipoCuenta plan, DateTime ahoraUtc)
    {
        if (plan == TipoCuenta.Individual) throw new ReglaDeNegocioException("El plan Individual es gratuito.");
        if (plan == TipoCuenta.Premium && EmpresaVigente(ahoraUtc)) throw new ReglaDeNegocioException("Ya tienes un plan Empresa vigente.");
        if (plan == TipoCuenta.Empresa && PremiumVigente(ahoraUtc)) throw new ReglaDeNegocioException("Tienes un plan Premium vigente; podrás pasar a Empresa cuando venza.");
    }

    public void ActivarPremium(DateTime ahoraUtc)
    {
        ValidarPuedeSuscribirse(TipoCuenta.Premium, ahoraUtc);
        Renovar(TipoCuenta.Premium, ahoraUtc);
        DestacadosGratisRestantes = PoliticaEcoPuntos.DestacadosGratisPremium; // se resetea en cada renovación pagada
    }

    public void ActivarEmpresa(DateTime ahoraUtc)
    {
        ValidarPuedeSuscribirse(TipoCuenta.Empresa, ahoraUtc);
        Renovar(TipoCuenta.Empresa, ahoraUtc);
        DestacadosGratisRestantes = PoliticaEcoPuntos.DestacadosGratisEmpresa; // se resetea en cada renovación pagada
    }

    private void Renovar(TipoCuenta plan, DateTime ahoraUtc)
    {
        var baseFecha = FechaVencimientoSuscripcion.HasValue && FechaVencimientoSuscripcion.Value > ahoraUtc ? FechaVencimientoSuscripcion.Value : ahoraUtc;
        TipoCuenta = plan;
        FechaVencimientoSuscripcion = baseFecha.AddDays(PoliticaEcoPuntos.DuracionSuscripcionDias);
    }

    public bool PuedeUsarDestacadoGratis(DateTime ahoraUtc) => PlanPagoVigente(ahoraUtc) && DestacadosGratisRestantes > 0;

    public void UsarDestacadoGratis(DateTime ahoraUtc)
    {
        if (!PlanPagoVigente(ahoraUtc)) throw new ReglaDeNegocioException("Los destacados gratis son un beneficio de los planes Premium y Empresa vigentes.");
        if (DestacadosGratisRestantes <= 0) throw new ReglaDeNegocioException("No te quedan destacados gratis este mes.");
        DestacadosGratisRestantes--;
    }

    // ---------------- Verificación de identidad ----------------
    /// <summary>Valida SIN modificar nada (se llama antes de cobrar).</summary>
    public void ValidarPuedeSolicitarVerificacion(string documentoUrl)
    {
        if (EstadoVerificacion == EstadoVerificacion.Aprobada) throw new ReglaDeNegocioException("Tu cuenta ya está verificada.");
        if (EstadoVerificacion == EstadoVerificacion.Pendiente) throw new ReglaDeNegocioException("Ya tienes una verificación en revisión.");
        if (!UrlsSeguras.EsValida(documentoUrl))
            throw new ReglaDeNegocioException("El documento debe ser una URL https válida.");
    }

    public void SolicitarVerificacion(string documentoUrl)
    {
        ValidarPuedeSolicitarVerificacion(documentoUrl);
        EstadoVerificacion = EstadoVerificacion.Pendiente;
        DocumentoVerificacionUrl = documentoUrl;
        MotivoRechazoVerificacion = null;
    }

    public void AprobarVerificacion()
    {
        if (EstadoVerificacion != EstadoVerificacion.Pendiente) throw new ReglaDeNegocioException("No hay una verificación pendiente para este usuario.");
        EstadoVerificacion = EstadoVerificacion.Aprobada;
        MotivoRechazoVerificacion = null;
        DocumentoVerificacionUrl = null; // minimización: una vez decidido, el documento de identidad ya no se conserva
    }

    public void RechazarVerificacion(string motivo)
    {
        if (EstadoVerificacion != EstadoVerificacion.Pendiente) throw new ReglaDeNegocioException("No hay una verificación pendiente para este usuario.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Debes indicar el motivo del rechazo.");
        EstadoVerificacion = EstadoVerificacion.Rechazada;
        MotivoRechazoVerificacion = motivo.Trim();
        DocumentoVerificacionUrl = null;
    }
}
