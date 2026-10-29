using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Pendiente → Aprobada | Rechazada. Una aprobada puede quedar Retirada (moderación). Rechazada o Retirada pueden volver a solicitar.</summary>
public enum EstadoFundacion { Pendiente = 1, Aprobada = 2, Rechazada = 3, Retirada = 4 }

/// <summary>
/// Perfil de fundación (entidad sin ánimo de lucro) de una cuenta. La cuenta sigue siendo una cuenta normal: la verificación solo
/// añade la insignia "Fundación verificada", la página pública y "Lo que necesitamos". La revisa un SuperUsuario con el
/// certificado de existencia y representación legal de la Cámara de Comercio, que vive en el bucket privado y se borra al decidir.
/// </summary>
public class Fundacion
{
    public const int LongitudDescripcion = 600;

    private Fundacion() { RazonSocial = ""; Nit = ""; Descripcion = ""; } // EF Core

    public Fundacion(Guid usuarioId, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        RazonSocial = ""; Nit = ""; Descripcion = "";
        FechaSolicitudUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string RazonSocial { get; private set; }
    /// <summary>NIT con dígito de verificación ("900123456-7"). Solo se muestra en la página pública si la fundación lo elige.</summary>
    public string Nit { get; private set; }
    public bool MostrarNit { get; private set; }
    /// <summary>Qué hace la fundación y a quién atiende (página pública).</summary>
    public string Descripcion { get; private set; }
    public EstadoFundacion Estado { get; private set; }
    /// <summary>Certificado de existencia (bucket privado). Solo existe mientras la solicitud está pendiente.</summary>
    public string? CertificadoUrl { get; private set; }
    public string? MotivoDecision { get; private set; }
    public DateTime FechaSolicitudUtc { get; private set; }
    public DateTime? FechaDecisionUtc { get; private set; }

    public bool EstaAprobada => Estado == EstadoFundacion.Aprobada;

    /// <summary>
    /// Calificada en el Régimen Tributario Especial (DIAN). La marca un SuperUsuario con la evidencia que revisó (nota). Solo se
    /// dice "pertenece al Régimen Tributario Especial": que una donación sea deducible depende de cada caso y no se promete.
    /// </summary>
    public bool RegimenTributarioEspecial { get; private set; }
    public string? NotaRegimen { get; private set; }
    public DateTime? RegimenRevisadoUtc { get; private set; }

    public void MarcarRegimenTributarioEspecial(bool calificada, string nota, DateTime ahoraUtc)
    {
        if (Estado != EstadoFundacion.Aprobada) throw new ReglaDeNegocioException("Primero verifica la fundación.");
        nota = (nota ?? "").Trim();
        if (nota.Length is < 5 or > 300) throw new ReglaDeNegocioException("Anota qué evidencia revisaste (entre 5 y 300 caracteres).");
        RegimenTributarioEspecial = calificada;
        NotaRegimen = nota;
        RegimenRevisadoUtc = ahoraUtc;
    }

    /// <summary>
    /// Persona jurídica: su NIT empieza por 8 o 9 (las cédulas y los NIT de personas naturales no). Es un filtro: la prueba real
    /// es el certificado de la Cámara de Comercio que revisa el SuperUsuario.
    /// </summary>
    public static string NormalizarNitEsal(string? nit)
    {
        var normalizado = DocumentosFiscales.NormalizarNit(nit);
        if (normalizado[0] is not ('8' or '9') || normalizado.Length < 11)
            throw new ReglaDeNegocioException("Escribe el NIT de la fundación (persona jurídica: empieza por 8 o 9 y tiene 9 dígitos más el de verificación).");
        return normalizado;
    }

    public void Solicitar(string razonSocial, string nit, string descripcion, bool mostrarNit, string certificadoUrl, DateTime ahoraUtc)
    {
        if (Estado == EstadoFundacion.Pendiente) throw new ReglaDeNegocioException("Ya tienes una solicitud en revisión.");
        if (Estado == EstadoFundacion.Aprobada) throw new ReglaDeNegocioException("Tu fundación ya está verificada.");
        if (!UrlsSeguras.EsValida(certificadoUrl)) throw new ReglaDeNegocioException("El certificado debe ser una URL https válida.");
        RazonSocial = ValidarRazonSocial(razonSocial);
        Nit = NormalizarNitEsal(nit);
        AplicarPerfil(descripcion, mostrarNit);
        CertificadoUrl = certificadoUrl;
        Estado = EstadoFundacion.Pendiente;
        MotivoDecision = null;
        FechaSolicitudUtc = ahoraUtc;
        FechaDecisionUtc = null;
    }

    /// <summary>Lo que la fundación puede cambiar sin volver a verificarse (razón social y NIT exigen una solicitud nueva).</summary>
    public void EditarPerfil(string descripcion, bool mostrarNit)
    {
        if (Estado is not (EstadoFundacion.Pendiente or EstadoFundacion.Aprobada))
            throw new ReglaDeNegocioException("Envía una solicitud nueva para actualizar los datos de la fundación.");
        AplicarPerfil(descripcion, mostrarNit);
    }

    public void Aprobar(DateTime ahoraUtc)
    {
        ExigirPendiente();
        Estado = EstadoFundacion.Aprobada;
        MotivoDecision = null;
        CertificadoUrl = null; // minimización: decidido, el certificado ya no se conserva
        FechaDecisionUtc = ahoraUtc;
    }

    public void Rechazar(string motivo, DateTime ahoraUtc)
    {
        ExigirPendiente();
        Estado = EstadoFundacion.Rechazada;
        MotivoDecision = ExigirMotivo(motivo);
        CertificadoUrl = null;
        FechaDecisionUtc = ahoraUtc;
    }

    /// <summary>Moderación: deja de ser una fundación verificada (p. ej. se disolvió o usó la insignia para engañar).</summary>
    public void Retirar(string motivo, DateTime ahoraUtc)
    {
        if (Estado != EstadoFundacion.Aprobada) throw new ReglaDeNegocioException("Esta fundación no está verificada.");
        Estado = EstadoFundacion.Retirada;
        RegimenTributarioEspecial = false; // al volver a verificarse se revisa de nuevo
        MotivoDecision = ExigirMotivo(motivo);
        FechaDecisionUtc = ahoraUtc;
    }

    private void ExigirPendiente()
    {
        if (Estado != EstadoFundacion.Pendiente) throw new ReglaDeNegocioException("No hay una solicitud de fundación pendiente para esta cuenta.");
    }

    private static string ExigirMotivo(string motivo)
    {
        motivo = (motivo ?? "").Trim();
        if (motivo.Length == 0) throw new ReglaDeNegocioException("Debes indicar el motivo.");
        return motivo.Length > 300 ? motivo[..300] : motivo;
    }

    private static string ValidarRazonSocial(string razonSocial)
    {
        var nombre = FormatosTexto.Compactar(razonSocial);
        if (nombre.Length is < 3 or > 150) throw new ReglaDeNegocioException("La razón social debe tener entre 3 y 150 caracteres.");
        if (!FormatosTexto.EsNombreComercial(nombre))
            throw new ReglaDeNegocioException("La razón social solo puede tener letras, números, espacios y & . , - ' ( ).");
        return nombre;
    }

    private void AplicarPerfil(string descripcion, bool mostrarNit)
    {
        descripcion = (descripcion ?? "").Trim();
        if (descripcion.Length is < 20 or > LongitudDescripcion)
            throw new ReglaDeNegocioException($"Cuenta qué hace la fundación en 20 a {LongitudDescripcion} caracteres.");
        if (!TextoSeguro.EsSeguro(descripcion, multilinea: true)) throw new ReglaDeNegocioException(TextoSeguro.Mensaje);
        Descripcion = descripcion;
        MostrarNit = mostrarNit;
    }
}

/// <summary>Algo que una fundación verificada necesita ("Lo que necesitamos"). La fundación la borra cuando se cubre.</summary>
public class NecesidadFundacion
{
    public const int MaxPorFundacion = 10;
    public const int LongitudMaxima = 100;
    public const int CantidadMaxima = 100_000;

    private NecesidadFundacion() { Texto = ""; } // EF Core

    public NecesidadFundacion(Guid fundacionId, string texto, int? categoriaId, int? cantidad, bool urgente, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        FundacionId = fundacionId;
        Texto = "";
        Editar(texto, categoriaId, cantidad, urgente);
        FechaUtc = ahoraUtc;
    }

    public Guid Id { get; private set; }
    public Guid FundacionId { get; private set; }
    public string Texto { get; private set; }
    public int? CategoriaId { get; private set; }
    /// <summary>Cantidad aproximada (opcional): "40 cuadernos".</summary>
    public int? Cantidad { get; private set; }
    public bool Urgente { get; private set; }
    public DateTime FechaUtc { get; private set; }
    /// <summary>Lo recibido: suma 1 por cada donación ligada a esta necesidad que se completa; la fundación lo puede corregir.</summary>
    public int Recibidos { get; private set; }
    /// <summary>Se cubrió: al llegar a la cantidad pedida o cuando la fundación lo indica. Sale de "Lo que piden las fundaciones".</summary>
    public DateTime? CubiertaUtc { get; private set; }
    public bool EstaCubierta => CubiertaUtc.HasValue;

    /// <summary>Una donación ligada a esta necesidad se entregó a la fundación.</summary>
    public void RegistrarRecibido(DateTime ahoraUtc) => AjustarProgreso(Recibidos + 1, cubierta: null, ahoraUtc);

    /// <summary>
    /// La fundación corrige lo recibido (una donación puede traer varias unidades) o marca la necesidad como cubierta.
    /// Con <paramref name="cubierta"/> null se cubre sola al llegar a la cantidad; false la vuelve a abrir.
    /// </summary>
    public void AjustarProgreso(int recibidos, bool? cubierta, DateTime ahoraUtc)
    {
        if (recibidos is < 0 or > CantidadMaxima) throw new ReglaDeNegocioException($"Lo recibido debe estar entre 0 y {CantidadMaxima:N0}.");
        Recibidos = recibidos;
        var completa = cubierta ?? (Cantidad is { } c && recibidos >= c);
        CubiertaUtc = completa ? CubiertaUtc ?? ahoraUtc : null;
    }

    public void Editar(string texto, int? categoriaId, int? cantidad, bool urgente)
    {
        texto = FormatosTexto.Compactar(texto);
        if (texto.Length is < 3 or > LongitudMaxima)
            throw new ReglaDeNegocioException($"Cuenta lo que necesitan en 3 a {LongitudMaxima} caracteres.");
        if (!TextoSeguro.EsSeguro(texto)) throw new ReglaDeNegocioException(TextoSeguro.Mensaje);
        if (cantidad is < 1 or > CantidadMaxima) throw new ReglaDeNegocioException($"La cantidad debe estar entre 1 y {CantidadMaxima:N0}.");
        Texto = texto;
        CategoriaId = categoriaId;
        Cantidad = cantidad;
        Urgente = urgente;
    }
}
