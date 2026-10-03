using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Common;

public sealed record EscalonDescuento(int PuntosMinimos, int DescuentoPorcentaje);

public sealed record CotizacionBeneficio(int PrecioBaseCop, int DescuentoPorcentaje, int PuntosACanjear, int TotalCop);

/// <summary>
/// ÚNICA fuente de verdad de la economía de Eco-Puntos y de los planes. El endpoint público
/// GET /api/v1/eco-puntos/politica lee de aquí (nada se hardcodea en los DTOs).
/// Reglas de oro: los Eco-Puntos nunca se convierten a pesos y nunca pagan al 100 % un beneficio con precio en pesos
/// (Destacar, Verificar, planes). El único uso "solo con puntos" es Impulsar, que no tiene precio en pesos.
/// </summary>
public static class PoliticaEcoPuntos
{
    // ---- Cómo se ganan ----
    public const int PuntosBienvenida = 10;
    public const decimal ReputacionMaxima = 5.0m;
    public const int MaxTransaccionesConPuntosPorDia = 5; // anti-farmeo
    /// <summary>Anti-farmeo: la misma pareja de personas solo suma puntos y reputación una vez en esta ventana.</summary>
    public const int DiasEntreTransaccionesConPuntosMismaPareja = 30;
    /// <summary>Anti-farmeo: una persona solo cuenta una calificación por cada otra persona en esta ventana.</summary>
    public const int DiasEntreCalificacionesMismaPareja = 30;

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

    // ---- Uso directo de los puntos ----
    /// <summary>Impulsar sube la publicación al primer lugar de "Más recientes". Se paga solo con Eco-Puntos.</summary>
    public const int PuntosImpulsar = 20;
    public const int HorasEntreImpulsos = 24;

    // ---- Beneficios pagados (COP, IVA incluido) ----
    public const int PrecioDestacarCop = 6_000;
    public const int DuracionDestacadoDias = 7;
    public const int PrecioVerificarCop = 20_000;
    public const int PrecioPremiumCop = 15_000;
    public const int PrecioEmpresaCop = 50_000;
    public const int DuracionSuscripcionDias = 30;
    public const int DestacadosGratisPremium = 3;
    public const int DescuentoPremiumPorcentaje = 15;
    public const int DestacadosGratisEmpresa = 10;
    public const int DescuentoEmpresaPorcentaje = 20;
    /// <summary>Días antes del vencimiento del plan en que se envía el recordatorio de renovación.</summary>
    public const int DiasRecordatorioVencimiento = 3;

    // ---- Límites por plan ----
    public const int MaxPublicacionesIndividual = 50;
    public const int MaxPublicacionesPremium = 150;
    public const int MaxPublicacionesEmpresa = 1_000;
    /// <summary>
    /// Estatuto del Consumidor (Ley 1480, art. 53): quien vende de forma habitual debe estar identificado.
    /// Más de este número de publicaciones de venta activas exige identidad verificada o plan Empresa.
    /// </summary>
    public const int MaxVentasActivasSinIdentificar = 5;

    public static int MaxPublicacionesActivas(TipoCuenta planEfectivo) => planEfectivo switch
    {
        TipoCuenta.Empresa => MaxPublicacionesEmpresa,
        TipoCuenta.Premium => MaxPublicacionesPremium,
        _ => MaxPublicacionesIndividual
    };

    public static int DescuentoPlan(TipoCuenta planEfectivo) => planEfectivo switch
    {
        TipoCuenta.Empresa => DescuentoEmpresaPorcentaje,
        TipoCuenta.Premium => DescuentoPremiumPorcentaje,
        _ => 0
    };

    public static int DestacadosGratisPorPlan(TipoCuenta plan) => plan switch
    {
        TipoCuenta.Empresa => DestacadosGratisEmpresa,
        TipoCuenta.Premium => DestacadosGratisPremium,
        _ => 0
    };

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
    /// el plan vigente suma su descuento (Premium 15 %, Empresa 20 %). Con el descuento máximo (60 %) siempre se paga algo.
    /// </summary>
    public static CotizacionBeneficio Cotizar(int precioBaseCop, IReadOnlyList<EscalonDescuento> escalones, int saldoPuntos, int descuentoPlanPorcentaje)
    {
        var escalon = escalones.OrderByDescending(e => e.PuntosMinimos).FirstOrDefault(e => saldoPuntos >= e.PuntosMinimos);
        var descuento = Math.Min(90, (escalon?.DescuentoPorcentaje ?? 0) + Math.Max(0, descuentoPlanPorcentaje));
        var puntos = escalon?.PuntosMinimos ?? 0;
        var total = (int)Math.Round(precioBaseCop * (100 - descuento) / 100m, MidpointRounding.AwayFromZero);
        return new CotizacionBeneficio(precioBaseCop, descuento, puntos, Math.Max(total, 1));
    }
}
