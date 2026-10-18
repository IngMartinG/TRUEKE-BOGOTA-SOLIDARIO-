using System.ComponentModel.DataAnnotations;
using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Negocio.Dtos;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>Cada campo acepta solo lo que le corresponde (mismas reglas en el DTO, en el dominio y en el front).</summary>
public class FormatosTextoTests
{
    [Theory]
    [InlineData("María José O'Neill")]
    [InlineData("Ana Pérez")]
    [InlineData("Jean-Paul Muñoz")]
    [InlineData("J. R. Güiza")]
    [InlineData("José O’Brien")]
    [InlineData("  Ana   Pérez  ")]
    public void Nombre_completo_valido(string nombre) => Assert.True(FormatosTexto.EsNombreCompleto(nombre));

    [Theory]
    [InlineData("Ana2 Pérez")]
    [InlineData("123")]
    [InlineData("Ana")]
    [InlineData("Ana_Pérez")]
    [InlineData("Ana -- Pérez")]
    [InlineData("Ana Pérez 😀")]
    [InlineData("'Ana Pérez")]
    [InlineData("Ana@ Pérez")]
    public void Nombre_completo_invalido(string nombre) => Assert.False(FormatosTexto.EsNombreCompleto(nombre));

    [Fact]
    public void Nombre_de_persona_admite_una_sola_palabra_pero_no_digitos()
    {
        Assert.True(FormatosTexto.EsNombrePersona("Ana"));
        Assert.False(FormatosTexto.EsNombrePersona("Ana2"));
    }

    [Theory]
    [InlineData("3M", true)]
    [InlineData("Muebles La 80", true)]
    [InlineData("Pérez & Hijos S.A.S.", true)]
    [InlineData("Tienda (Centro), Ltda.", true)]
    [InlineData("Tienda <script>", false)]
    [InlineData("Tienda_Bogotá", false)]
    [InlineData("&&&", false)]
    public void Nombre_comercial(string nombre, bool valido) => Assert.Equal(valido, FormatosTexto.EsNombreComercial(nombre));

    [Theory]
    [InlineData("20 de Julio", true)]
    [InlineData("Usaquén", true)]
    [InlineData("Sector 3 #2", true)]
    [InlineData("Barrio N° 5", true)]
    [InlineData("123", false)]
    [InlineData("Kennedy!", false)]
    [InlineData("Chapinero/Centro", false)]
    public void Localidad(string localidad, bool valida) => Assert.Equal(valida, FormatosTexto.EsLocalidad(localidad));

    [Theory]
    [InlineData("Calle 10 # 20-30", true)]
    [InlineData("Cra. 7 N° 45-10, apto 301", true)]
    [InlineData("Km 5 vía La Calera 2/3", true)]
    [InlineData("12345", false)]
    [InlineData("Calle 10 <b>", false)]
    public void Direccion(string direccion, bool valida) => Assert.Equal(valida, FormatosTexto.EsDireccion(direccion));

    [Theory]
    [InlineData("900123456", true)]
    [InlineData("900.123.456-8", true)]
    [InlineData("900A23456", false)]
    [InlineData("NIT900123456", false)]
    [InlineData("900123456-X", false)]
    public void Nit_solo_admite_digitos_puntos_y_dv(string nit, bool valido)
        => Assert.Equal(valido, System.Text.RegularExpressions.Regex.IsMatch(nit, FormatosTexto.Nit));

    [Theory]
    [InlineData("Hola\u202Emundo")]   // invierte el sentido del texto
    [InlineData("Ho\u200Bla")]        // espacio de ancho cero
    [InlineData("Hola\uFEFF")]
    [InlineData("Hola\u2066x\u2069")]
    [InlineData("Hola\u0007")]
    public void Texto_seguro_rechaza_control_e_invisibles(string texto)
    {
        Assert.False(TextoSeguro.EsSeguro(texto));
        Assert.False(TextoSeguro.EsSeguro(texto, multilinea: true));
    }

    [Fact]
    public void Texto_seguro_permite_emojis_y_saltos_de_linea_solo_si_es_multilinea()
    {
        Assert.True(TextoSeguro.EsSeguro("Familia 👨‍👩‍👧 y bandera 🇨🇴, ñandú"));
        Assert.True(TextoSeguro.EsSeguro("Línea 1\r\nLínea 2", multilinea: true));
        Assert.False(TextoSeguro.EsSeguro("Línea 1\nLínea 2"));
    }

    [Fact]
    public void El_dominio_rechaza_nombres_y_localidades_fuera_de_formato()
    {
        Assert.Throws<ReglaDeNegocioException>(() => new Usuario("Ana2 Pérez", "Kennedy", "a@t.co", "Clave12345"));
        Assert.Throws<ReglaDeNegocioException>(() => new Usuario("Ana", "Kennedy", "a@t.co", "Clave12345"));
        Assert.Throws<ReglaDeNegocioException>(() => new Usuario("Ana Pérez", "123", "a@t.co", "Clave12345"));
        var u = new Usuario("  Ana   Pérez ", "20 de Julio", "a@t.co", "Clave12345");
        Assert.Equal("Ana Pérez", u.NombreCompleto);
    }

    [Theory]
    [InlineData("Ana Bolena", "Ana Bolena", false)]
    [InlineData("MARÍA JOSÉ O'NEILL", "María José O'Neill", false)]
    [InlineData("ana de la torre", "Ana de la Torre", false)]
    [InlineData("jean-paul  muñoz", "Jean-Paul Muñoz", false)]
    [InlineData("Ana 🌸 Pérez", "Ana Pérez", false)]
    [InlineData("Laura_Gómez99", "Laura Gómez", false)]
    [InlineData("Ana", "Ana", true)]                         // nombre real pero sin apellido: debe completarlo
    [InlineData("🌸🌸 123", Usuario.NombreProvisional, true)]
    [InlineData("", Usuario.NombreProvisional, true)]
    public void Google_crea_la_cuenta_con_el_nombre_real_bien_escrito(string deGoogle, string esperado, bool pendiente)
    {
        var u = Usuario.CrearDesdeGoogle(deGoogle, "g@t.co", "sub-1", DateTime.UtcNow);
        Assert.Equal(esperado, u.NombreCompleto);
        Assert.Equal(pendiente, u.NombreRealPendiente);
    }

    [Fact]
    public void Con_nombre_y_apellido_validos_ya_no_queda_pendiente()
    {
        var u = Usuario.CrearDesdeGoogle("🌸", "g@t.co", "sub-1", DateTime.UtcNow);
        Assert.True(u.NombreRealPendiente);
        u.ActualizarPerfil("Ana Pérez", "Chapinero");
        Assert.False(u.NombreRealPendiente);
    }

    [Fact]
    public void Datos_de_factura_exigen_formato_al_crear_pero_se_leen_los_ya_guardados()
    {
        Assert.Throws<ReglaDeNegocioException>(() => DatosComprador.Crear(TipoDocumentoFiscal.CC, "1234567", "Ana2 Pérez", "a@t.co", "Calle 10 # 20-30", "11001"));
        Assert.Throws<ReglaDeNegocioException>(() => DatosComprador.Crear(TipoDocumentoFiscal.CC, "12A4567", "Ana Pérez", "a@t.co", "Calle 10 # 20-30", "11001"));
        Assert.Throws<ReglaDeNegocioException>(() => DatosComprador.Crear(TipoDocumentoFiscal.CC, "1234567", "Ana Pérez", "a@t.co", "Calle 10 <b>", "11001"));
        Assert.Equal("Muebles La 80 S.A.S.", DatosComprador.Crear(TipoDocumentoFiscal.NIT, "900123456", "Muebles La 80 S.A.S.", "a@t.co", "Calle 10 # 20-30", "11001").Nombre);
        Assert.Equal("Ana2 Pérez", DatosComprador.Restaurar(TipoDocumentoFiscal.CC, "1234567", "Ana2 Pérez", "a@t.co", "Calle 10 # 20-30", "11001").Nombre);
    }

    [Theory]
    [InlineData(TipoDocumentoFiscalDto.CC, "1.234.567", "Ana Pérez", true)]
    [InlineData(TipoDocumentoFiscalDto.CC, "12A4567", "Ana Pérez", false)]
    [InlineData(TipoDocumentoFiscalDto.CE, "AB12345", "Ana Pérez", true)]
    [InlineData(TipoDocumentoFiscalDto.Pasaporte, "AB-12345", "Ana Pérez", false)]
    [InlineData(TipoDocumentoFiscalDto.NIT, "900123456", "3M Colombia S.A.", true)]
    [InlineData(TipoDocumentoFiscalDto.NIT, "900A23456", "3M Colombia S.A.", false)]
    [InlineData(TipoDocumentoFiscalDto.CC, "1234567", "Ana2", false)]
    public void Facturacion_valida_documento_y_nombre_segun_el_tipo(TipoDocumentoFiscalDto tipo, string documento, string nombre, bool valido)
    {
        var r = new DatosFacturacionRequest { TipoDocumento = tipo, Documento = documento, Nombre = nombre, Correo = "a@t.co", Direccion = "Calle 10 # 20-30", MunicipioCodigo = "11001" };
        var errores = new List<ValidationResult>();
        Assert.Equal(valido, Validator.TryValidateObject(r, new ValidationContext(r), errores, validateAllProperties: true));
    }
}
