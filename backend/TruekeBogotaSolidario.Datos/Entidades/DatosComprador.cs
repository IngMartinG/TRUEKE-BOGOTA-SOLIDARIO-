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

    public static DatosComprador Crear(TipoDocumentoFiscal tipo, string? documento, string? nombre, string? correo, string? direccion, string? municipioCodigo)
    {
        if (!Enum.IsDefined(tipo)) throw new ReglaDeNegocioException("Tipo de documento no válido.");
        var doc = DocumentosFiscales.Normalizar(tipo, documento ?? "");
        var n = (nombre ?? "").Trim();
        if (n.Length is < 3 or > 150 || n.Any(char.IsControl)) throw new ReglaDeNegocioException("El nombre o razón social debe tener entre 3 y 150 caracteres.");
        var c = Usuario.NormalizarCorreo(correo ?? "");
        if (c.Length is < 5 or > 160 || !c.Contains('@')) throw new ReglaDeNegocioException("El correo para la factura no es válido.");
        var dir = (direccion ?? "").Trim();
        if (dir.Length is < 5 or > 150 || dir.Any(char.IsControl)) throw new ReglaDeNegocioException("La dirección es obligatoria para la factura (entre 5 y 150 caracteres).");
        if (string.IsNullOrWhiteSpace(municipioCodigo)) throw new ReglaDeNegocioException("El municipio es obligatorio para la factura.");
        var mpio = Divipola.Exigir(municipioCodigo).Codigo;
        return new DatosComprador(tipo, doc, n, c, dir, mpio);
    }
}
