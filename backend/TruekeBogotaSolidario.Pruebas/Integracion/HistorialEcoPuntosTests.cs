using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TruekeBogotaSolidario.Pruebas.Infraestructura;

namespace TruekeBogotaSolidario.Pruebas.Integracion;

public class HistorialEcoPuntosTests : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;
    public HistorialEcoPuntosTests(FabricaApi fabrica) => _fabrica = fabrica;

    private static async Task<JsonElement> MovimientosAsync(HttpClient c)
        => await c.GetFromJsonAsync<JsonElement>("/api/v1/eco-puntos/movimientos?pagina=1&tamano=20");

    [Fact]
    public async Task Cada_cambio_del_saldo_queda_en_el_historial_y_la_suma_coincide_con_el_saldo()
    {
        var c = await Api.RegistrarAsync(_fabrica, "historial");

        // Bienvenida al verificar el correo y bono de perfil completo al poner la foto (ya tenía localidad)
        var m = await MovimientosAsync(c);
        var iniciales = m.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new (string?, int)[] { ("PerfilCompleto", 30), ("Bienvenida", 10) },
            iniciales.Select(i => (i.GetProperty("concepto").GetString(), i.GetProperty("puntos").GetInt32())));

        // Recarga aprobada de un monto sin paquete (sin bono): +120
        var ini = await c.PostAsJsonAsync("/api/v1/pagos/iniciar", new { concepto = "Recarga", montoRecargaCop = 12000, facturaANombre = false, aceptoConsumidorFinal = true });
        Assert.Equal(HttpStatusCode.Created, ini.StatusCode);
        var referencia = (await ini.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("referencia").GetString();
        (await c.PostAsync($"/api/v1/pagos/{referencia}/simular?aprobado=true", null)).EnsureSuccessStatusCode();

        // Impulsar: -20, con el título de la publicación como detalle
        var pub = await Api.CrearPublicacionAsync(c);
        (await c.PostAsync($"/api/v1/publicaciones/{pub}/impulsar", null)).EnsureSuccessStatusCode();

        m = await MovimientosAsync(c);
        var items = m.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, m.GetProperty("total").GetInt32());
        Assert.Equal(new[] { "Impulsar", "Recarga", "PerfilCompleto", "Bienvenida" }, items.Select(i => i.GetProperty("concepto").GetString()));
        Assert.Equal(-20, items[0].GetProperty("puntos").GetInt32());
        Assert.Equal(pub, items[0].GetProperty("referenciaId").GetGuid());
        Assert.Equal(120, items[1].GetProperty("puntos").GetInt32());

        var saldo = (await c.GetFromJsonAsync<JsonElement>("/api/v1/eco-puntos/resumen")).GetProperty("saldo").GetInt32();
        Assert.Equal(saldo, items.Sum(i => i.GetProperty("puntos").GetInt32()));
        Assert.Equal(saldo, items[0].GetProperty("saldoResultante").GetInt32());
    }

    [Fact]
    public async Task El_historial_es_privado_y_exige_sesion()
    {
        var a = await Api.RegistrarAsync(_fabrica, "privada");
        var b = await Api.RegistrarAsync(_fabrica, "ajena");
        Assert.Equal(2, (await MovimientosAsync(a)).GetProperty("total").GetInt32());
        Assert.Equal(2, (await MovimientosAsync(b)).GetProperty("total").GetInt32()); // cada quien ve solo su bienvenida y su perfil completo
        Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().GetAsync("/api/v1/eco-puntos/movimientos")).StatusCode);
    }
}
