namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>Cálculos geográficos sobre la esfera terrestre (suficiente precisión para distancias urbanas).</summary>
public static class Geo
{
    public const double RadioTierraKm = 6371.0088;

    /// <summary>Decimales con que se muestran las coordenadas a quien no puede verlas exactas (~1,1 km de imprecisión).</summary>
    public const int DecimalesAproximados = 2;

    /// <summary>Distancia de círculo máximo (fórmula de Haversine) en kilómetros.</summary>
    public static double DistanciaKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ARadianes(lat2 - lat1);
        var dLon = ARadianes(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ARadianes(lat1)) * Math.Cos(ARadianes(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * RadioTierraKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>Rectángulo lat/lon que contiene el círculo (prefiltro barato en SQL antes de Haversine).</summary>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) Caja(double lat, double lon, double radioKm, double margenGrados = 0)
    {
        var dLat = radioKm / RadioTierraKm * (180 / Math.PI) + margenGrados;
        var cosLat = Math.Max(Math.Cos(ARadianes(lat)), 0.01); // evita dividir por ~0 cerca de los polos
        var dLon = dLat / cosLat + margenGrados;
        return (Math.Max(lat - dLat, -90), Math.Min(lat + dLat, 90), Math.Max(lon - dLon, -180), Math.Min(lon + dLon, 180));
    }

    public static double Aproximar(double coordenada) => Math.Round(coordenada, DecimalesAproximados, MidpointRounding.AwayFromZero);

    private static double ARadianes(double grados) => grados * Math.PI / 180;
}
