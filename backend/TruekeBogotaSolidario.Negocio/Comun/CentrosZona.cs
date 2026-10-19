using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Punto de referencia de una publicación sin punto de entrega (es opcional): el centro de su localidad en Bogotá o, en
/// otro municipio, el centro del municipio. Son datos públicos (la localidad y el municipio ya se muestran), así que no
/// revelan nada del dueño. Los centros de las localidades son los mismos del front (CENTRO_LOCALIDAD en api/tipos.ts).
/// </summary>
public static class CentrosZona
{
    private static readonly Dictionary<string, (double Lat, double Lon)> Localidades = new Dictionary<string, (double, double)>
    {
        ["Usaquén"] = (4.7031, -74.0306),
        ["Chapinero"] = (4.6486, -74.0628),
        ["Santa Fe"] = (4.6097, -74.0697),
        ["San Cristóbal"] = (4.5636, -74.0833),
        ["Usme"] = (4.4767, -74.1172),
        ["Tunjuelito"] = (4.5761, -74.1339),
        ["Bosa"] = (4.6181, -74.1903),
        ["Kennedy"] = (4.6286, -74.1525),
        ["Fontibón"] = (4.6783, -74.1428),
        ["Engativá"] = (4.7072, -74.1103),
        ["Suba"] = (4.7411, -74.0836),
        ["Barrios Unidos"] = (4.6672, -74.0753),
        ["Teusaquillo"] = (4.6403, -74.0889),
        ["Los Mártires"] = (4.6036, -74.0889),
        ["Antonio Nariño"] = (4.5867, -74.1006),
        ["Puente Aranda"] = (4.6192, -74.1153),
        ["La Candelaria"] = (4.5964, -74.0733),
        ["Rafael Uribe Uribe"] = (4.5711, -74.1147),
        ["Ciudad Bolívar"] = (4.5089, -74.1528),
        ["Sumapaz"] = (4.0317, -74.2678),
    }.ToDictionary(x => Divipola.Normalizar(x.Key), x => x.Value);

    /// <summary>
    /// Distancia máxima (km) entre el centro de un municipio y una publicación suya que se le asigna: Bogotá mide unos
    /// 30 km de norte a sur sin Sumapaz. Sirve para prefiltrar municipios antes de calcular cada distancia.
    /// </summary>
    public const double AlcanceMunicipioKm = 40;

    /// <summary>Centro de la localidad (Bogotá) o del municipio; null si el municipio no existe en DIVIPOLA.</summary>
    public static (double Lat, double Lon)? Centro(string municipioCodigo, string? localidad)
    {
        if (municipioCodigo == Divipola.CodigoBogota && !string.IsNullOrWhiteSpace(localidad)
            && Localidades.TryGetValue(Divipola.Normalizar(localidad), out var centro))
            return centro;
        var m = Divipola.ObtenerMunicipio(municipioCodigo);
        return m is null ? null : (m.Latitud, m.Longitud);
    }

    /// <summary>Municipios cuyas publicaciones pueden quedar a menos de <paramref name="radioKm"/> del punto.</summary>
    public static IReadOnlyList<string> MunicipiosCerca(double lat, double lon, double radioKm)
        => Divipola.Municipios.Where(m => Geo.DistanciaKm(lat, lon, m.Latitud, m.Longitud) <= radioKm + AlcanceMunicipioKm)
            .Select(m => m.Codigo).ToList();
}
