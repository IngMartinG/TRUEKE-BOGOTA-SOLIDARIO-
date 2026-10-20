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
    /// <summary>Los Eco-Puntos de perfil completo (foto, correo verificado y localidad) se dan una sola vez.</summary>
    public bool BonoPerfilCompletoOtorgado { get; private set; }
    /// <summary>Los Eco-Puntos de la primera publicación (activa 24 h o intercambiada) se dan una sola vez.</summary>
    public bool BonoPrimeraPublicacionOtorgado { get; private set; }

    // Perfil de empresa (solo se muestra con el plan Empresa vigente)
    public string? NombreComercial { get; private set; }
    public string? Nit { get; private set; }
    /// <summary>La empresa decide si su NIT se ve en el perfil público (si es persona natural, el NIT es su cédula).</summary>
    public bool MostrarNit { get; private set; }

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
    /// <summary>Total ganado participando (define el nivel): solo sube, no cuenta lo recargado y gastar no lo baja.</summary>
    public int PuntosParticipacion { get; private set; }
    public NivelEcoPuntos Nivel => PoliticaEcoPuntos.NivelPara(PuntosParticipacion);
    public decimal Reputacion { get; private set; }
    public int TotalTruekesCompletados { get; private set; }
    public int TotalComprasRealizadas { get; private set; }
    public int TotalDonacionesRealizadas { get; private set; }

    // Rol y plan
    public RolUsuarioEnum Rol { get; private set; }
    public TipoCuenta TipoCuenta { get; private set; }
    public DateTime? FechaVencimientoSuscripcion { get; private set; }
    public int DestacadosGratisRestantes { get; private set; }
    /// <summary>Cuándo se repone el cupo de destacados gratis (cada 30 días mientras el plan esté vigente; no se acumula).</summary>
    public DateTime? ProximaRecargaDestacadosUtc { get; private set; }
    /// <summary>Cuándo activó la prueba gratis de Premium (una sola vez por cuenta).</summary>
    public DateTime? PruebaPremiumUsadaUtc { get; private set; }
    /// <summary>El Premium vigente es la prueba gratis (sin pago); al pagar un plan deja de serlo.</summary>
    public bool EnPruebaPremium { get; private set; }

    // Invita y ganan los dos
    /// <summary>Código propio para invitar (corto y legible, único). Se crea la primera vez que la persona lo pide.</summary>
    public string? CodigoInvitacion { get; private set; }
    /// <summary>Quién invitó a esta persona (solo al crear la cuenta).</summary>
    public Guid? InvitadoPorId { get; private set; }
    /// <summary>Cuándo se cerró la invitación: su primer intercambio válido ya dio la recompensa (una sola vez).</summary>
    public DateTime? RecompensaInvitacionUtc { get; private set; }

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
    /// <summary>Versión de los términos de uso aceptada y cuándo.</summary>
    public string? TerminosVersion { get; private set; }
    public DateTime? FechaAceptacionTerminos { get; private set; }
    public DateTime? FechaAceptacionPolitica { get; private set; }
    /// <summary>
    /// Solo se usa para comprobar la mayoría de edad (la plataforma es para mayores de 18). Nunca se muestra a otras personas.
    /// null = cuenta anterior a la regla: no puede actuar hasta registrarla.
    /// </summary>
    public DateOnly? FechaNacimiento { get; private set; }
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

    /// <summary>Token de concurrencia optimista: la columna de sistema xmin de PostgreSQL.</summary>
    public uint RowVersion { get; private set; }

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
        var n = FormatosTexto.Compactar(nombreCompleto);
        var l = FormatosTexto.Compactar(localidad);
        if (n.Length is < 3 or > 120) throw new ReglaDeNegocioException("El nombre debe tener entre 3 y 120 caracteres.");
        if (l.Length is < 2 or > 60) throw new ReglaDeNegocioException("La localidad debe tener entre 2 y 60 caracteres.");
        if (!TextoSeguro.EsSeguro(n) || !TextoSeguro.EsSeguro(l)) throw new ReglaDeNegocioException(TextoSeguro.Mensaje);
        if (!FormatosTexto.EsNombreCompleto(n)) throw new ReglaDeNegocioException(FormatosTexto.MensajeNombreCompleto);
        if (!FormatosTexto.EsLocalidad(l)) throw new ReglaDeNegocioException(FormatosTexto.MensajeLocalidad);
        return (n, l);
    }

    public void ActualizarPerfil(string nombreCompleto, string localidad, string? municipioCodigo = null)
    {
        (NombreCompleto, Localidad) = ValidarPerfil(nombreCompleto, localidad);
        if (municipioCodigo is not null) MunicipioCodigo = Divipola.Exigir(municipioCodigo).Codigo;
        EvaluarPerfilCompleto();
    }

    /// <summary>Localidad de las cuentas creadas con Google (y de las eliminadas) hasta que la persona escriba la suya.</summary>
    public const string LocalidadSinEspecificar = "Sin especificar";

    /// <summary>Foto de perfil, correo verificado y localidad: lo que da los Eco-Puntos de perfil completo.</summary>
    public bool PerfilCompleto => FotoUrl is not null && CorreoVerificado && !EstaEliminado
        && !string.IsNullOrWhiteSpace(Localidad) && Localidad != LocalidadSinEspecificar;

    /// <summary>
    /// Se llama al cambiar cualquiera de los requisitos: al completar el último se acreditan los puntos, una sola vez
    /// (quitar la foto y volver a ponerla no los repite). Devuelve true si los acreditó ahora.
    /// </summary>
    public bool EvaluarPerfilCompleto()
    {
        if (BonoPerfilCompletoOtorgado || !PerfilCompleto) return false;
        BonoPerfilCompletoOtorgado = true;
        AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosPerfilCompleto, ConceptoEcoPuntos.PerfilCompleto);
        return true;
    }

    /// <summary>
    /// Una publicación propia siguió activa 24 h o se intercambió con puntos: una sola vez por persona. Devuelve true si los acreditó ahora.
    /// </summary>
    public bool RecibirBonoPrimeraPublicacion(Guid publicacionId, string titulo)
    {
        if (BonoPrimeraPublicacionOtorgado || EstaEliminado) return false;
        BonoPrimeraPublicacionOtorgado = true;
        AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosPrimeraPublicacion, ConceptoEcoPuntos.PrimeraPublicacion, publicacionId, titulo);
        return true;
    }

    /// <summary>La URL ya debe venir validada (archivo propio y limpio). Devuelve la foto anterior para borrarla.</summary>
    public string? CambiarFoto(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 500) throw new ReglaDeNegocioException("La foto no es válida.");
        var anterior = FotoUrl;
        FotoUrl = url;
        EvaluarPerfilCompleto();
        return anterior;
    }

    public string? QuitarFoto()
    {
        var anterior = FotoUrl;
        FotoUrl = null;
        return anterior;
    }

    /// <summary>
    /// Nombre comercial (en las publicaciones y el perfil) y NIT registrado mientras el plan Empresa esté vigente.
    /// El número de NIT solo se muestra en el perfil público si la empresa lo elige.
    /// </summary>
    public void ActualizarPerfilEmpresa(string nombreComercial, string nit, bool mostrarNit, DateTime ahoraUtc)
    {
        if (!EmpresaVigente(ahoraUtc)) throw new ReglaDeNegocioException("El perfil de empresa está disponible con el plan Empresa vigente.");
        var nombre = FormatosTexto.Compactar(nombreComercial);
        if (nombre.Length is < 2 or > 120) throw new ReglaDeNegocioException("El nombre comercial debe tener entre 2 y 120 caracteres.");
        if (!FormatosTexto.EsNombreComercial(nombre)) throw new ReglaDeNegocioException(FormatosTexto.MensajeNombreComercial);
        NombreComercial = nombre;
        Nit = DocumentosFiscales.NormalizarNit(nit);
        MostrarNit = mostrarNit;
    }

    // ---------------- Facturación electrónica ----------------
    /// <summary>Tiene TODOS los datos que la DIAN exige para facturar a su nombre (los guardados antes sin dirección no cuentan).</summary>
    public bool TieneDatosFacturacion => DatosFacturacion is not null;

    /// <summary>Los datos fiscales completos, o null si faltan (se factura a consumidor final si así lo elige).</summary>
    public DatosComprador? DatosFacturacion
    {
        get
        {
            if (FacturacionTipoDocumento is not { } tipo || string.IsNullOrWhiteSpace(FacturacionDireccion) || string.IsNullOrWhiteSpace(FacturacionMunicipioCodigo))
                return null;
            try { return DatosComprador.Restaurar(tipo, FacturacionDocumento, FacturacionNombre, FacturacionCorreo, FacturacionDireccion, FacturacionMunicipioCodigo); }
            catch (ReglaDeNegocioException) { return null; }
        }
    }

    public void ActualizarDatosFacturacion(TipoDocumentoFiscal tipo, string documento, string nombre, string correo, string? direccion, string? municipioCodigo)
    {
        var d = DatosComprador.Crear(tipo, documento, nombre, correo, direccion, municipioCodigo); // todos obligatorios
        FacturacionTipoDocumento = d.TipoDocumento;
        FacturacionDocumento = d.Documento;
        FacturacionNombre = d.Nombre;
        FacturacionCorreo = d.Correo;
        FacturacionDireccion = d.Direccion;
        FacturacionMunicipioCodigo = d.MunicipioCodigo;
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

    // ---------------- Registro de oferentes (Ley 1480, art. 53) ----------------
    // El portal de contacto lleva un registro de quienes venden: nombre, documento, dirección de notificaciones y teléfono.
    // Documento, dirección y teléfono se guardan cifrados (los cifra Negocio con Seguridad:ClaveCifrado). Nunca se muestran a
    // terceros: solo al titular y a la moderación al resolver la PQRS de alguien que le compró.
    public TipoDocumentoFiscal? OferenteTipoDocumento { get; private set; }
    public string? OferenteNombre { get; private set; }
    public string? OferenteDocumentoCifrado { get; private set; }
    public string? OferenteDireccionCifrada { get; private set; }
    public string? OferenteMunicipioCodigo { get; private set; }
    public string? OferenteTelefonoCifrado { get; private set; }
    public DateTime? OferenteRegistradoUtc { get; private set; }

    /// <summary>Puede publicar en modo Compra: registró sus datos de oferente.</summary>
    public bool RegistroOferenteCompleto => OferenteRegistradoUtc is not null && OferenteTipoDocumento is not null
        && !string.IsNullOrWhiteSpace(OferenteDocumentoCifrado) && !string.IsNullOrWhiteSpace(OferenteDireccionCifrada)
        && !string.IsNullOrWhiteSpace(OferenteTelefonoCifrado);

    /// <summary>Los valores ya vienen validados y cifrados. Se puede actualizar; la fecha es la del primer registro.</summary>
    public void RegistrarOferente(TipoDocumentoFiscal tipo, string nombre, string documentoCifrado, string direccionCifrada,
        string municipioCodigo, string telefonoCifrado, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(documentoCifrado) || string.IsNullOrWhiteSpace(direccionCifrada)
            || string.IsNullOrWhiteSpace(municipioCodigo) || string.IsNullOrWhiteSpace(telefonoCifrado))
            throw new ReglaDeNegocioException("Completa todos los datos del registro de vendedor.");
        OferenteTipoDocumento = tipo;
        OferenteNombre = FormatosTexto.Compactar(nombre);
        OferenteDocumentoCifrado = documentoCifrado;
        OferenteDireccionCifrada = direccionCifrada;
        OferenteMunicipioCodigo = municipioCodigo;
        OferenteTelefonoCifrado = telefonoCifrado;
        OferenteRegistradoUtc ??= ahoraUtc;
    }

    public void BorrarRegistroOferente()
    {
        OferenteTipoDocumento = null;
        OferenteNombre = null;
        OferenteDocumentoCifrado = null;
        OferenteDireccionCifrada = null;
        OferenteMunicipioCodigo = null;
        OferenteTelefonoCifrado = null;
        OferenteRegistradoUtc = null;
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
            AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosBienvenida, ConceptoEcoPuntos.Bienvenida);
            BonoBienvenidaOtorgado = true;
        }
        EvaluarPerfilCompleto();
    }

    public void AceptarPoliticaDatos(string version, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 20) throw new ReglaDeNegocioException("Versión de política no válida.");
        PoliticaDatosVersion = version;
        FechaAceptacionPolitica = ahoraUtc;
    }

    /// <summary>Aceptación de los términos de uso (separada de la autorización de datos, como exige la Ley 1581).</summary>
    public void AceptarTerminos(string version, DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(version) || version.Length > 20) throw new ReglaDeNegocioException("Versión de términos no válida.");
        TerminosVersion = version;
        FechaAceptacionTerminos = ahoraUtc;
    }

    /// <summary>Aceptó las versiones vigentes de la política de datos y de los términos.</summary>
    public bool LegalAlDia(string versionPolitica, string versionTerminos)
        => PoliticaDatosVersion == versionPolitica && TerminosVersion == versionTerminos;

    /// <summary>Edad mínima para usar la plataforma (términos de uso).</summary>
    public const int EdadMinima = 18;
    /// <summary>Más que esto es casi seguro un error de digitación.</summary>
    public const int EdadMaxima = 120;

    /// <summary>Colombia no tiene horario de verano: la fecha local es siempre UTC-5.</summary>
    public static DateOnly HoyEnColombia(DateTime ahoraUtc) => DateOnly.FromDateTime(ahoraUtc.AddHours(-5));

    public static int EdadEn(DateOnly nacimiento, DateOnly hoy)
    {
        var edad = hoy.Year - nacimiento.Year;
        if (hoy < nacimiento.AddYears(edad)) edad--; // aún no cumple años este año (quien nació un 29 de febrero cumple el 28 en años no bisiestos)
        return edad;
    }

    /// <summary>Rechaza a menores de edad y fechas imposibles. Una vez registrada no se cambia (evita "corregirla" para pasar el filtro).</summary>
    public static void ValidarFechaNacimiento(DateOnly fecha, DateOnly hoy)
    {
        if (fecha > hoy) throw new ReglaDeNegocioException("La fecha de nacimiento no puede ser futura.");
        var edad = EdadEn(fecha, hoy);
        if (edad > EdadMaxima) throw new ReglaDeNegocioException("Revisa la fecha de nacimiento: no parece correcta.");
        if (edad < EdadMinima) throw new ReglaDeNegocioException($"Trueke Bogotá Solidario es solo para personas mayores de {EdadMinima} años.");
    }

    public void RegistrarFechaNacimiento(DateOnly fecha, DateOnly hoy)
    {
        if (FechaNacimiento is not null) throw new ReglaDeNegocioException("La fecha de nacimiento ya está registrada.");
        if (MenorDetectado) throw new ReglaDeNegocioException("La cuenta está en revisión: declaró una fecha de menor de edad.");
        ValidarFechaNacimiento(fecha, hoy);
        FechaNacimiento = fecha;
    }

    public bool MayoriaEdadConfirmada => FechaNacimiento is not null;

    /// <summary>
    /// Una cuenta ya existente declaró una fecha de nacimiento de menor de edad: queda en revisión y no puede volver a
    /// intentarlo con otra fecha. Solo conserva su sesión, la exportación y la eliminación de sus datos y la PQRS.
    /// </summary>
    public DateTime? MenorDetectadoUtc { get; private set; }

    public bool MenorDetectado => MenorDetectadoUtc is not null;

    /// <summary>Fecha válida (no futura) de alguien que aún no cumple <see cref="EdadMinima"/> años.</summary>
    public static bool EsFechaDeMenor(DateOnly fecha, DateOnly hoy) => fecha <= hoy && EdadEn(fecha, hoy) < EdadMinima;

    public void MarcarMenorDetectado(DateTime ahoraUtc)
    {
        if (MayoriaEdadConfirmada) throw new ReglaDeNegocioException("La cuenta ya registró su fecha de nacimiento.");
        MenorDetectadoUtc ??= ahoraUtc;
        VersionSeguridad++; // los tokens vigentes dicen "edad pendiente": se emiten de nuevo con la marca
    }

    /// <summary>
    /// La moderación comprobó que fue un error de digitación (por ejemplo, con un documento enviado por PQRS): la cuenta
    /// puede registrar su fecha de nuevo.
    /// </summary>
    public void DescartarMenorDetectado()
    {
        if (!MenorDetectado) throw new ReglaDeNegocioException("La cuenta no está en revisión por mayoría de edad.");
        MenorDetectadoUtc = null;
        VersionSeguridad++;
    }

    /// <summary>Nombre temporal de una cuenta de Google cuyo nombre no se pudo usar (solo emojis, dígitos…).</summary>
    public const string NombreProvisional = "Usuario Trueke";

    /// <summary>
    /// Falta el nombre real (nombre y apellido con el formato de <see cref="FormatosTexto.NombreCompleto"/>): la cuenta solo
    /// puede explorar hasta escribirlo en su perfil. Pasa con el nombre provisional, con nombres de Google de una sola palabra
    /// y con cuentas guardadas antes de la regla.
    /// </summary>
    public bool NombreRealPendiente => NombreCompleto == NombreProvisional || !FormatosTexto.EsNombreCompleto(NombreCompleto);

    /// <summary>Cuenta creada con Google: sin contraseña (no se puede entrar con clave hasta que el usuario cree una) y correo ya verificado.</summary>
    public static Usuario CrearDesdeGoogle(string nombre, string correo, string googleSub, DateTime ahoraUtc)
    {
        // El nombre real de Google, bien escrito ("MARÍA PÉREZ" → "María Pérez"). Si no queda nada usable, uno provisional;
        // en ambos casos, si no es nombre y apellido, la cuenta solo explora hasta completarlo (NombreRealPendiente).
        var nombreValido = FormatosTexto.EscribirNombre(nombre) ?? NombreProvisional;
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NombreCompleto = nombreValido,
            Localidad = LocalidadSinEspecificar,
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
        Localidad = LocalidadSinEspecificar;
        Correo = $"eliminado-{Id:N}@trueke.invalid";
        CorreoCanonico = Correo;
        ClaveHash = "";
        GoogleSub = null;
        NombreComercial = null;
        Nit = null;
        MostrarNit = false;
        BorrarDatosFacturacion();
        BorrarRegistroOferente();
        DocumentoVerificacionUrl = null;
        MotivoRechazoVerificacion = null;
        FotoUrl = null;
        SaldoEcoPuntos = 0;
        PuntosParticipacion = 0;
        DestacadosGratisRestantes = 0;
        ProximaRecargaDestacadosUtc = null;
        EnPruebaPremium = false;
        CodigoInvitacion = null; // el enlace deja de funcionar
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

    // ---------------- Invitaciones ----------------
    public void AsignarCodigoInvitacion(string codigo)
    {
        if (CodigoInvitacion is not null) return;
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length is < 6 or > 12 || !codigo.All(char.IsAsciiLetterOrDigit))
            throw new ReglaDeNegocioException("Código de invitación no válido.");
        CodigoInvitacion = codigo;
    }

    /// <summary>Solo al crear la cuenta y una sola vez. Nadie se invita a sí mismo ni lo invita una cuenta eliminada.</summary>
    public void RegistrarInvitador(Usuario invitador)
    {
        if (InvitadoPorId is not null || invitador.Id == Id || invitador.EstaEliminado) return;
        InvitadoPorId = invitador.Id;
    }

    public bool EsperaRecompensaInvitacion => InvitadoPorId is not null && RecompensaInvitacionUtc is null && !EstaEliminado;

    /// <summary>Primer intercambio válido de la persona invitada: recibe sus puntos y la invitación queda cerrada.</summary>
    public void RecibirRecompensaInvitacion(DateTime ahoraUtc, Guid solicitudId)
    {
        if (!EsperaRecompensaInvitacion) return;
        RecompensaInvitacionUtc = ahoraUtc;
        AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosInvitacion, ConceptoEcoPuntos.Invitacion, solicitudId, "Te invitaron y completaste tu primer intercambio");
    }

    // ---------------- Eco-Puntos ----------------
    // Cada cambio del saldo deja un MovimientoEcoPuntos (historial): no hay otra forma de tocar SaldoEcoPuntos.
    private readonly List<MovimientoEcoPuntos> _movimientosEcoPuntos = new();
    public IReadOnlyList<MovimientoEcoPuntos> MovimientosEcoPuntos => _movimientosEcoPuntos;

    public void AcreditarEcoPuntos(int puntos, ConceptoEcoPuntos concepto, Guid? referenciaId = null, string? detalle = null)
    {
        if (puntos <= 0) throw new ReglaDeNegocioException("Los puntos a acreditar deben ser positivos.");
        SaldoEcoPuntos = checked(SaldoEcoPuntos + puntos);
        if (PoliticaEcoPuntos.CuentaParaNivel(concepto)) PuntosParticipacion = checked(PuntosParticipacion + puntos);
        _movimientosEcoPuntos.Add(new MovimientoEcoPuntos(Id, puntos, concepto, referenciaId, detalle, SaldoEcoPuntos));
    }

    public void DebitarEcoPuntos(int puntos, ConceptoEcoPuntos concepto, Guid? referenciaId = null, string? detalle = null)
    {
        if (puntos <= 0) throw new ReglaDeNegocioException("Los puntos a debitar deben ser positivos.");
        if (puntos > SaldoEcoPuntos) throw new ReglaDeNegocioException("Saldo de Eco-Puntos insuficiente.");
        SaldoEcoPuntos -= puntos;
        _movimientosEcoPuntos.Add(new MovimientoEcoPuntos(Id, -puntos, concepto, referenciaId, detalle, SaldoEcoPuntos));
    }

    /// <summary>El contador siempre sube; los puntos y la reputación solo si <paramref name="otorgarPuntos"/> (tope anti-farmeo).</summary>
    public void RegistrarTransaccionCompletada(ModoTransaccion modo, bool otorgarPuntos = true, Guid? solicitudId = null, string? detalle = null)
    {
        switch (modo)
        {
            case ModoTransaccion.Trueke: TotalTruekesCompletados++; break;
            case ModoTransaccion.Compra: TotalComprasRealizadas++; break;
            case ModoTransaccion.Donacion: TotalDonacionesRealizadas++; break;
            default: throw new ReglaDeNegocioException("Modo de transacción no válido.");
        }
        if (!otorgarPuntos) return;
        AcreditarEcoPuntos(PoliticaEcoPuntos.PuntosPorModo(modo), ConceptoEcoPuntos.Intercambio, solicitudId, detalle);
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
    /// Reembolso de un plan (retracto o reversión): se descuentan los días pagados (30 o 365). Si el plan queda vencido,
    /// se pierden los destacados gratis restantes.
    /// </summary>
    public void RevertirPlan(TipoCuenta plan, int dias, DateTime ahoraUtc)
    {
        if (TipoCuenta != plan || FechaVencimientoSuscripcion is null) return;
        FechaVencimientoSuscripcion = FechaVencimientoSuscripcion.Value.AddDays(-dias);
        if (FechaVencimientoSuscripcion <= ahoraUtc)
        {
            FechaVencimientoSuscripcion = ahoraUtc;
            DestacadosGratisRestantes = 0;
            ProximaRecargaDestacadosUtc = null;
        }
    }

    /// <summary>Reembolso de una recarga: se retiran los puntos acreditados que aún queden (el saldo nunca queda negativo).</summary>
    public int RetirarEcoPuntosHasta(int puntos, Guid? pagoId = null)
    {
        var retirados = Math.Clamp(puntos, 0, SaldoEcoPuntos);
        if (retirados > 0) DebitarEcoPuntos(retirados, ConceptoEcoPuntos.ReembolsoRecarga, pagoId);
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
        if (plan == TipoCuenta.Empresa && PremiumVigente(ahoraUtc) && !EnPruebaPremium)
            throw new ReglaDeNegocioException("Tienes un plan Premium vigente; podrás pasar a Empresa cuando venza.");
    }

    /// <summary>La prueba gratis es para quien nunca tuvo un plan (ni la usó): una vez por cuenta.</summary>
    public bool PuedeUsarPruebaPremium => PruebaPremiumUsadaUtc is null && FechaVencimientoSuscripcion is null;

    public bool PruebaPremiumVigente(DateTime ahoraUtc) => EnPruebaPremium && PremiumVigente(ahoraUtc);

    /// <summary>
    /// Premium gratis por unos días: todo Premium salvo los destacados gratis (trae menos). Sin tarjeta ni cobro al terminar:
    /// cuando vence vuelve al plan Individual como cualquier plan.
    /// </summary>
    public void ActivarPruebaPremium(DateTime ahoraUtc)
    {
        if (PruebaPremiumUsadaUtc is not null) throw new ReglaDeNegocioException("Ya usaste tu prueba gratis de Premium.");
        if (FechaVencimientoSuscripcion is not null) throw new ReglaDeNegocioException("La prueba gratis es para cuentas que nunca han tenido un plan.");
        PruebaPremiumUsadaUtc = ahoraUtc;
        EnPruebaPremium = true;
        TipoCuenta = TipoCuenta.Premium;
        FechaVencimientoSuscripcion = ahoraUtc.AddDays(PoliticaEcoPuntos.DiasPruebaPremium);
        DestacadosGratisRestantes = PoliticaEcoPuntos.DestacadosGratisPruebaPremium;
        ProximaRecargaDestacadosUtc = null; // la prueba no repone el cupo
    }

    /// <param name="dias">30 el mensual, 365 el anual.</param>
    public void ActivarPremium(DateTime ahoraUtc, int dias = PoliticaEcoPuntos.DuracionSuscripcionDias)
    {
        ValidarPuedeSuscribirse(TipoCuenta.Premium, ahoraUtc);
        Renovar(TipoCuenta.Premium, ahoraUtc, dias);
    }

    /// <param name="dias">30 el mensual, 365 el anual.</param>
    public void ActivarEmpresa(DateTime ahoraUtc, int dias = PoliticaEcoPuntos.DuracionSuscripcionDias)
    {
        ValidarPuedeSuscribirse(TipoCuenta.Empresa, ahoraUtc);
        Renovar(TipoCuenta.Empresa, ahoraUtc, dias);
    }

    /// <summary>
    /// Renovar con el plan vigente solo suma días (mensual o anual): el cupo de destacados y su próxima reposición siguen igual
    /// (no se pierde nada). Un plan nuevo (o tras vencer) arranca con el cupo completo y lo repone cada 30 días, también en el anual.
    /// </summary>
    private void Renovar(TipoCuenta plan, DateTime ahoraUtc, int dias)
    {
        if (dias <= 0) throw new ReglaDeNegocioException("Duración del plan no válida.");
        var mismoPlan = PlanEfectivo(ahoraUtc) == plan;
        // Pagar durante la prueba: se suman los días que le quedaban y arranca el cupo completo del plan pagado
        var vigente = mismoPlan && !EnPruebaPremium;
        var baseFecha = mismoPlan ? FechaVencimientoSuscripcion!.Value : ahoraUtc;
        TipoCuenta = plan;
        EnPruebaPremium = false;
        FechaVencimientoSuscripcion = baseFecha.AddDays(dias);
        if (!vigente)
        {
            DestacadosGratisRestantes = PoliticaEcoPuntos.DestacadosGratisPorPlan(plan);
            ProximaRecargaDestacadosUtc = ahoraUtc.AddDays(PoliticaEcoPuntos.DiasCicloDestacadosGratis);
        }
    }

    /// <summary>Destacados gratis disponibles hoy: si ya pasó la fecha de reposición, el cupo completo del plan.</summary>
    public int DestacadosGratisDisponibles(DateTime ahoraUtc)
    {
        if (!PlanPagoVigente(ahoraUtc)) return 0;
        return ProximaRecargaDestacadosUtc is { } proxima && proxima <= ahoraUtc
            ? PoliticaEcoPuntos.DestacadosGratisPorPlan(PlanEfectivo(ahoraUtc))
            : DestacadosGratisRestantes;
    }

    /// <summary>Próxima reposición del cupo vista hoy (si la fecha guardada ya pasó, la del ciclo siguiente).</summary>
    public DateTime? ProximaRecargaDestacadosVigente(DateTime ahoraUtc)
    {
        if (!PlanPagoVigente(ahoraUtc) || ProximaRecargaDestacadosUtc is not { } proxima) return null;
        while (proxima <= ahoraUtc) proxima = proxima.AddDays(PoliticaEcoPuntos.DiasCicloDestacadosGratis);
        return proxima;
    }

    /// <summary>Aplica la reposición pendiente (si toca) y deja la próxima fecha en el siguiente ciclo de 30 días.</summary>
    private void ReponerDestacadosSiToca(DateTime ahoraUtc)
    {
        if (!PlanPagoVigente(ahoraUtc) || ProximaRecargaDestacadosUtc is not { } proxima || proxima > ahoraUtc) return;
        DestacadosGratisRestantes = PoliticaEcoPuntos.DestacadosGratisPorPlan(PlanEfectivo(ahoraUtc));
        while (proxima <= ahoraUtc) proxima = proxima.AddDays(PoliticaEcoPuntos.DiasCicloDestacadosGratis);
        ProximaRecargaDestacadosUtc = proxima;
    }

    public bool PuedeUsarDestacadoGratis(DateTime ahoraUtc) => DestacadosGratisDisponibles(ahoraUtc) > 0;

    public void UsarDestacadoGratis(DateTime ahoraUtc)
    {
        if (!PlanPagoVigente(ahoraUtc)) throw new ReglaDeNegocioException("Los destacados gratis son un beneficio de los planes Premium y Empresa vigentes.");
        ReponerDestacadosSiToca(ahoraUtc);
        if (DestacadosGratisRestantes <= 0) throw new ReglaDeNegocioException("Ya usaste los destacados gratis de este ciclo: se reponen cada 30 días.");
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
