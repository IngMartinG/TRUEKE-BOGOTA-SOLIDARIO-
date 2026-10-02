using TruekeBogotaSolidario.Negocio.Comun;

namespace TruekeBogotaSolidario.Pruebas.Unitarias;

public class GeoTests
{
    [Fact]
    public void Haversine_misma_coordenada_es_cero()
        => Assert.Equal(0, Geo.DistanciaKm(4.6, -74.08, 4.6, -74.08), 6);

    [Fact]
    public void Haversine_Bogota_Medellin_aprox_240_km()
    {
        // Plaza de Bolívar (Bogotá) → Parque Berrío (Medellín)
        var d = Geo.DistanciaKm(4.5981, -74.0758, 6.2518, -75.5636);
        Assert.InRange(d, 235, 250);
    }

    [Fact]
    public void Haversine_un_grado_de_latitud_aprox_111_km()
        => Assert.InRange(Geo.DistanciaKm(0, 0, 1, 0), 110.5, 111.5);

    [Fact]
    public void Caja_contiene_el_circulo()
    {
        var (minLat, maxLat, minLon, maxLon) = Geo.Caja(4.65, -74.05, 5);
        Assert.True(Geo.DistanciaKm(4.65, -74.05, maxLat, -74.05) >= 4.99);
        Assert.True(Geo.DistanciaKm(4.65, -74.05, 4.65, minLon) >= 4.99);
        Assert.True(minLat < 4.65 && minLon < -74.05 && maxLon > -74.05);
    }

    [Fact]
    public void Aproximar_redondea_a_dos_decimales()
    {
        Assert.Equal(4.61, Geo.Aproximar(4.6097102));
        Assert.Equal(-74.08, Geo.Aproximar(-74.0817500));
    }
}
