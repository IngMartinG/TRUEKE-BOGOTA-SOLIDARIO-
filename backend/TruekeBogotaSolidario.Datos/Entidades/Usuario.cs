using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

public class Usuario
{
    private Usuario() { NombreCompleto = ""; Localidad = ""; Correo = ""; ClaveHash = ""; } // EF Core

    public Usuario(string nombreCompleto, string localidad, string correo, string claveEnClaro)
    {
        Id = Guid.NewGuid();
        (NombreCompleto, Localidad) = ValidarPerfil(nombreCompleto, localidad);
        Correo = NormalizarCorreo(correo);
        if (Correo.Length is 0 or > 160 || !Correo.Contains('@')) throw new ReglaDeNegocioException("El correo no es válido.");
        ClaveHash = "";
        EstablecerClave(claveEnClaro);
        Rol = RolUsuarioEnum.Cliente;
        TipoCuenta = TipoCuenta.Individual;
        EstadoVerificacion = EstadoVerificacion.NoVerificado;
        FechaRegistro = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string NombreCompleto { get; private set; }
    public string Localidad { get; private set; }
    public string Correo { get; private set; }
    public string ClaveHash { get; private set; }
    public DateTime FechaRegistro { get; private set; }

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

    /// <summary>Token de concurrencia optimista (solo SQL Server).</summary>
    public byte[]? RowVersion { get; private set; }

    public bool EsVerificado => EstadoVerificacion == EstadoVerificacion.Aprobada;

    // ---------------- Perfil ----------------
    public static string NormalizarCorreo(string correo) => (correo ?? "").Trim().ToLowerInvariant();

    private static (string nombre, string localidad) ValidarPerfil(string nombreCompleto, string localidad)
    {
        var n = (nombreCompleto ?? "").Trim();
        var l = (localidad ?? "").Trim();
        if (n.Length is < 3 or > 120) throw new ReglaDeNegocioException("El nombre debe tener entre 3 y 120 caracteres.");
        if (l.Length is < 2 or > 60) throw new ReglaDeNegocioException("La localidad debe tener entre 2 y 60 caracteres.");
        if (n.Any(char.IsControl) || l.Any(char.IsControl)) throw new ReglaDeNegocioException("Los datos contienen caracteres no permitidos.");
        return (n, l);
    }

    public void ActualizarPerfil(string nombreCompleto, string localidad)
        => (NombreCompleto, Localidad) = ValidarPerfil(nombreCompleto, localidad);

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
    public void MarcarCorreoVerificado(DateTime ahoraUtc)
    {
        if (CorreoVerificado) return;
        CorreoVerificado = true;
        FechaVerificacionCorreo = ahoraUtc;
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
            Localidad = "Bogotá",
            Correo = NormalizarCorreo(correo),
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
        Localidad = "Bogotá";
        Correo = $"eliminado-{Id:N}@trueke.invalid";
        ClaveHash = "";
        GoogleSub = null;
        DocumentoVerificacionUrl = null;
        MotivoRechazoVerificacion = null;
        SaldoEcoPuntos = 0;
        DestacadosGratisRestantes = 0;
        IntentosFallidosLogin = 0;
        BloqueadoHasta = null;
        CorreoVerificado = false;
        EstaEliminado = true;
        FechaEliminacion = ahoraUtc;
        VersionSeguridad++; // todos los tokens de acceso dejan de valer
    }

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
        DestacadosGratisRestantes = 0;
    }

    private void Renovar(TipoCuenta plan, DateTime ahoraUtc)
    {
        var baseFecha = FechaVencimientoSuscripcion.HasValue && FechaVencimientoSuscripcion.Value > ahoraUtc ? FechaVencimientoSuscripcion.Value : ahoraUtc;
        TipoCuenta = plan;
        FechaVencimientoSuscripcion = baseFecha.AddDays(PoliticaEcoPuntos.DuracionSuscripcionDias);
    }

    public bool PuedeUsarDestacadoGratis(DateTime ahoraUtc) => PremiumVigente(ahoraUtc) && DestacadosGratisRestantes > 0;

    public void UsarDestacadoGratis(DateTime ahoraUtc)
    {
        if (!PremiumVigente(ahoraUtc)) throw new ReglaDeNegocioException("Tu plan Premium no está vigente.");
        if (DestacadosGratisRestantes <= 0) throw new ReglaDeNegocioException("No te quedan destacados gratis este mes.");
        DestacadosGratisRestantes--;
    }

    // ---------------- Verificación de identidad ----------------
    /// <summary>Valida SIN modificar nada (se llama antes de cobrar).</summary>
    public void ValidarPuedeSolicitarVerificacion(string documentoUrl)
    {
        if (EstadoVerificacion == EstadoVerificacion.Aprobada) throw new ReglaDeNegocioException("Tu cuenta ya está verificada.");
        if (EstadoVerificacion == EstadoVerificacion.Pendiente) throw new ReglaDeNegocioException("Ya tienes una verificación en revisión.");
        if (documentoUrl is null || documentoUrl.Length > 500
            || !Uri.TryCreate(documentoUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
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
    }

    public void RechazarVerificacion(string motivo)
    {
        if (EstadoVerificacion != EstadoVerificacion.Pendiente) throw new ReglaDeNegocioException("No hay una verificación pendiente para este usuario.");
        if (string.IsNullOrWhiteSpace(motivo)) throw new ReglaDeNegocioException("Debes indicar el motivo del rechazo.");
        EstadoVerificacion = EstadoVerificacion.Rechazada;
        MotivoRechazoVerificacion = motivo.Trim();
    }
}
