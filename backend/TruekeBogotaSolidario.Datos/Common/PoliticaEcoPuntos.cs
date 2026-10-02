using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Common;

public sealed record EscalonDescuento(int PuntosMinimos, int DescuentoPorcentaje);

public sealed record CotizacionBeneficio(int PrecioBaseCop, int DescuentoPorcentaje, int PuntosACanjear, int TotalCop);

/// <summary>
/// ÚNICA fuente de verdad de la economía de Eco-Puntos. El endpoint público
/// GET /api/v1/eco-puntos/politica lee de aquí (nada se hardcodea en los DTOs).
/// Reglas de oro: los Eco-Puntos nunca se convierten a pesos y nunca pagan un beneficio al 100 %.
/// </summary>
public static class PoliticaEcoPuntos
{
    // ---- Cómo se ganan ----
    public const int PuntosBienvenida = 10;
    public const decimal ReputacionMaxima = 5.0m;
    public const int MaxTransaccionesConPuntosPorDia = 5; // anti-farmeo

    public static int PuntosPorModo(ModoTransaccion modo) => modo switch
    {
        ModoTransaccion.Compra => 5,
        ModoTransaccion.Trueke => 10,
        ModoTransaccion.Donacion => 20,
        _ => 0
    };

    public static decimal ReputacionPorModo(ModoTransaccion modo) => modo switch
    {
        ModoTransaccion.Compra => 0.05m,
        ModoTransaccion.Trueke => 0.10m,
        ModoTransaccion.Donacion => 0.20m,
        _ => 0m
    };

    // ---- Beneficios pagados (COP) ----
    public const int PrecioDestacarCop = 6_000;
    public const int DuracionDestacadoDias = 7;
    public const int PrecioVerificarCop = 20_000;
    public const int PrecioPremiumCop = 15_000;
    public const int PrecioEmpresaCop = 50_000;
    public const int DuracionSuscripcionDias = 30;
    public const int DestacadosGratisPremium = 3;
    public const int DescuentoPremiumPorcentaje = 15;

    public static readonly IReadOnlyList<EscalonDescuento> EscalonesDestacar =
        new[] { new EscalonDescuento(500, 40), new EscalonDescuento(200, 25) };

    public static readonly IReadOnlyList<EscalonDescuento> EscalonesVerificar =
        new[] { new EscalonDescuento(500, 35), new EscalonDescuento(200, 20) };

    // ---- Recarga con dinero real (entra a la plataforma vía Wompi; nunca sale) ----
    public const int CopPorEcoPunto = 100;
    public const int RecargaMinimaCop = 2_000;
    public const int RecargaMaximaCop = 500_000;

    /// <summary>
    /// Calcula el precio final. Se canjean los puntos del escalón alcanzado (200 o 500) a cambio del descuento;
    /// si el usuario es Premium vigente suma 15 % adicional. Con descuento máximo (55 %) siempre se paga algo.
    /// </summary>
    public static CotizacionBeneficio Cotizar(int precioBaseCop, IReadOnlyList<EscalonDescuento> escalones, int saldoPuntos, bool premiumVigente)
    {
        var escalon = escalones.OrderByDescending(e => e.PuntosMinimos).FirstOrDefault(e => saldoPuntos >= e.PuntosMinimos);
        var descuento = (escalon?.DescuentoPorcentaje ?? 0) + (premiumVigente ? DescuentoPremiumPorcentaje : 0);
        var puntos = escalon?.PuntosMinimos ?? 0;
        var total = (int)Math.Round(precioBaseCop * (100 - descuento) / 100m, MidpointRounding.AwayFromZero);
        return new CotizacionBeneficio(precioBaseCop, descuento, puntos, Math.Max(total, 1));
    }
}
