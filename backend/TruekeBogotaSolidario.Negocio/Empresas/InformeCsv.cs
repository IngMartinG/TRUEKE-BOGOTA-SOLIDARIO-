using System.Globalization;
using System.Text;
using TruekeBogotaSolidario.Negocio.Servicios;

namespace TruekeBogotaSolidario.Negocio.Empresas;

/// <summary>Trazabilidad del informe en CSV (";", como facturas.csv): una fila por objeto entregado, las mismas que el PDF.</summary>
public static class InformeCsv
{
    public static string Generar(ContenidoInforme c)
    {
        var sb = new StringBuilder();
        Csv.Fila(sb, "Intercambio", "Fecha", "Campana", "Localidad", "Modo", "Categoria", "Material", "Kg", "OrigenPeso",
            "FactorKgCo2PorKg", "KgCo2", "VersionMetodologia", "EvidenciaFotografica");
        foreach (var e in c.Entregas)
            Csv.Fila(sb, e.Codigo, e.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), e.Campana, e.Localidad, e.Modo, e.Categoria,
                e.Material, e.Kg, e.OrigenPeso, e.FactorKgCo2PorKg.ToString("0.000", CultureInfo.InvariantCulture), e.KgCo2, e.Version,
                e.Evidencia ? "Sí" : "No");
        return sb.ToString();
    }
}
