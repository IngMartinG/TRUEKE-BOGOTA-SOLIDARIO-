using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>Validación de documentos de identificación para la factura electrónica (DIAN).</summary>
public static class DocumentosFiscales
{
    private static readonly int[] PesosNit = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };

    /// <summary>Dígito de verificación del NIT (algoritmo módulo 11 de la DIAN).</summary>
    public static int DigitoVerificacion(string nitSinDv)
    {
        var suma = 0;
        for (var i = 0; i < nitSinDv.Length; i++)
            suma += (nitSinDv[nitSinDv.Length - 1 - i] - '0') * PesosNit[i];
        var residuo = suma % 11;
        return residuo is 0 or 1 ? residuo : 11 - residuo;
    }

    /// <summary>
    /// Acepta "900.123.456-7", "9001234567" (con DV al final solo si viene separado por guion) o "900123456" (calcula el DV).
    /// Devuelve "900123456-7". Si el DV enviado no coincide, lanza ReglaDeNegocioException.
    /// </summary>
    public static string NormalizarNit(string? nit)
    {
        var texto = (nit ?? "").Trim().Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        var partes = texto.Split('-');
        if (partes.Length > 2) throw new ReglaDeNegocioException("El NIT no es válido.");
        var numero = partes[0];
        if (numero.Length is < 6 or > 15 || !numero.All(char.IsAsciiDigit)) throw new ReglaDeNegocioException("El NIT debe tener entre 6 y 15 dígitos.");
        var dv = DigitoVerificacion(numero);
        if (partes.Length == 2 && (partes[1].Length != 1 || !char.IsAsciiDigit(partes[1][0]) || partes[1][0] - '0' != dv))
            throw new ReglaDeNegocioException("El dígito de verificación del NIT no es correcto.");
        return $"{numero}-{dv}";
    }

    public static string Normalizar(TipoDocumentoFiscal tipo, string? documento)
    {
        var d = (documento ?? "").Trim().Replace(".", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        switch (tipo)
        {
            case TipoDocumentoFiscal.NIT:
                return NormalizarNit(d);
            case TipoDocumentoFiscal.CC:
                if (d.Length is < 5 or > 10 || !d.All(char.IsAsciiDigit)) throw new ReglaDeNegocioException("La cédula debe tener entre 5 y 10 dígitos.");
                return d;
            case TipoDocumentoFiscal.CE:
            case TipoDocumentoFiscal.Pasaporte:
                if (d.Length is < 3 or > 20 || !d.All(char.IsAsciiLetterOrDigit)) throw new ReglaDeNegocioException("El documento debe tener entre 3 y 20 letras o números.");
                return d.ToUpperInvariant();
            default:
                throw new ReglaDeNegocioException("Tipo de documento no válido.");
        }
    }
}
