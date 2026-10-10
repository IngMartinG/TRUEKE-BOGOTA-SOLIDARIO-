using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using TruekeBogotaSolidario.Presentacion.Monitoreo;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

/// <summary>El monitoreo nunca debe enviar tokens ni llaves que viajen en la URL.</summary>
public class TelemetriaTests
{
    [Fact]
    public void Quita_la_query_string_de_todas_las_URLs_antes_de_exportar()
    {
        using var actividad = new Activity("prueba");
        actividad.SetTag("url.query", "?access_token=eyJ.secreto");
        actividad.SetTag("url.full", "https://api.trueke.co/hubs/notificaciones?access_token=eyJ.secreto&id=1");
        actividad.SetTag("http.url", "https://sandbox.wompi.co/v1/merchants/pub_test_x?llave=privada");
        actividad.SetTag("http.target", "/hubs/notificaciones?access_token=eyJ.secreto");
        actividad.SetTag("url.path", "/hubs/notificaciones");

        new QuitarDatosSensibles().OnEnd(actividad);

        Assert.Null(actividad.GetTagItem("url.query"));
        Assert.Equal("https://api.trueke.co/hubs/notificaciones", actividad.GetTagItem("url.full"));
        Assert.Equal("https://sandbox.wompi.co/v1/merchants/pub_test_x", actividad.GetTagItem("http.url"));
        Assert.Equal("/hubs/notificaciones", actividad.GetTagItem("http.target"));
        Assert.Equal("/hubs/notificaciones", actividad.GetTagItem("url.path"));
        Assert.DoesNotContain(actividad.TagObjects, t => t.Value?.ToString()?.Contains("secreto") == true);
    }

    [Fact]
    public void Solo_se_activa_con_cadena_de_conexion()
    {
        var sin = WebApplication.CreateBuilder();
        Assert.False(Telemetria.Configurar(sin));

        var con = WebApplication.CreateBuilder();
        con.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://localhost/",
            ["Monitoreo:Muestreo"] = "0.5"
        });
        Assert.True(Telemetria.Configurar(con));
    }
}
