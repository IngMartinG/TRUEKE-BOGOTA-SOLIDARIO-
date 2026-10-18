using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Datos del adquirente para la factura electrónica a su nombre. Todos son obligatorios (los exige la DIAN): tipo y número
/// de documento, nombre o razón social, correo, dirección y municipio. Solo se construye validado y normalizado.
/// </summary>
public sealed record DatosComprador
{
    private DatosComprador(TipoDocumentoFiscal tipo, string documento, string nombre, string correo, string direccion, string municipioCodigo)
    {
        TipoDocumento = tipo; Documento = documento; Nombre = nombre; Correo = correo; Direccion = direccion; MunicipioCodigo = municipioCodigo;
    }

    public TipoDocumentoFiscal TipoDocumento { get; }
    public string Documento { get; }
    public string Nombre { get; }
    public string Correo { get; }
    public string Direccion { get; }
    public string MunicipioCodigo { get; }

    /// <summary>
    /// Datos nuevos: además del largo, exige el formato de cada campo (nombre de persona o razón social si es NIT, dirección).
    /// </summary>
    public static DatosComprador Crear(TipoDocumentoFiscal tipo, string? documento, string? nombre, string? correo, string? direccion, string? municipioCodigo)
        => Construir(tipo, documento, nombre, correo, direccion, municipioCodigo, exigirFormato: true);

    /// <summary>
    /// Datos ya guardados (perfil o pago): se leen con las reglas de largo de siempre para que los registros anteriores al
    /// catálogo de formatos no se pierdan. El formato se exige al volver a guardarlos.
    /// </summary>
    public static DatosComprador Restaurar(TipoDocumentoFiscal tipo, string? documento, string? nombre, string? correo, string? direccion, string? municipioCodigo)
        => Construir(tipo, documento, nombre, correo, direccion, municipioCodigo, exigirFormato: false);

    private static DatosComprador Construir(TipoDocumentoFiscal tipo, string? documento, string? nombre, string? correo, string? direccion, string? municipioCodigo, bool exigirFormato)
    {
        if (!Enum.IsDefined(tipo)) throw new ReglaDeNegocioException("Tipo de documento no válido.");
        var doc = DocumentosFiscales.Normalizar(tipo, documento ?? "");
        var n = FormatosTexto.Compactar(nombre);
        if (n.Length is < 3 or > 150 || !TextoSeguro.EsSeguro(n)) throw new ReglaDeNegocioException("El nombre o razón social debe tener entre 3 y 150 caracteres.");
        if (exigirFormato && tipo == TipoDocumentoFiscal.NIT && !FormatosTexto.EsNombreComercial(n))
            throw new ReglaDeNegocioException(FormatosTexto.MensajeRazonSocial);
        if (exigirFormato && tipo != TipoDocumentoFiscal.NIT && !FormatosTexto.EsNombrePersona(n))
            throw new ReglaDeNegocioException(FormatosTexto.MensajeNombrePersona);
        var c = Usuario.NormalizarCorreo(correo ?? "");
        if (c.Length is < 5 or > 160 || !c.Contains('@')) throw new ReglaDeNegocioException("El correo para la factura no es válido.");
        var dir = FormatosTexto.Compactar(direccion);
        if (dir.Length is < 5 or > 150 || !TextoSeguro.EsSeguro(dir)) throw new ReglaDeNegocioException("La dirección es obligatoria para la factura (entre 5 y 150 caracteres).");
        if (exigirFormato && !FormatosTexto.EsDireccion(dir)) throw new ReglaDeNegocioException(FormatosTexto.MensajeDireccion);
        if (string.IsNullOrWhiteSpace(municipioCodigo)) throw new ReglaDeNegocioException("El municipio es obligatorio para la factura.");
        var mpio = Divipola.Exigir(municipioCodigo).Codigo;
        return new DatosComprador(tipo, doc, n, c, dir, mpio);
    }
}
