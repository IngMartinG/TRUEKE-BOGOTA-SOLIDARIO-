using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Datos.Common;

/// <summary>
/// Precio de Destacar o Verificar: <paramref name="DescuentoPorcentaje"/> es el del plan; <paramref name="PuntosACanjear"/> los que la
/// persona eligió usar (1 punto = <see cref="PoliticaEcoPuntos.CopPorEcoPunto"/> COP); <paramref name="PuntosMaximos"/> los que puede usar.
/// </summary>
public sealed record CotizacionBeneficio(int PrecioBaseCop, int DescuentoPorcentaje, int PuntosACanjear, int TotalCop,
    int PrecioConPlanCop, int PuntosMaximos, int PuntosParaCubrirTodo);

/// <summary>
/// ÚNICA fuente de verdad de la economía de Eco-Puntos y de los planes. El endpoint público
/// GET /api/v1/eco-puntos/politica lee de aquí (nada se hardcodea en los DTOs).
/// Reglas de oro: los Eco-Puntos nunca se convierten a pesos ni salen de la plataforma. Valen lo mismo que en la recarga
/// (1 punto = 100 COP) y la persona ELIGE cuántos usar en Destacar y Verificar (pueden cubrirlo todo). Los planes se pagan en pesos.
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
    /// <summary>Cada cuánto se repone el cupo de destacados gratis de Premium y Empresa (no se acumula).</summary>
    public const int DiasCicloDestacadosGratis = 30;

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

    // ---- Recarga con dinero real (entra a la plataforma vía Wompi; nunca sale) ----
    public const int CopPorEcoPunto = 100;
    public const int RecargaMinimaCop = 2_000;
    public const int RecargaMaximaCop = 500_000;
    /// <summary>Lo mínimo que se cobra con la pasarela: con puntos, el resto por pagar es 0 (todo con puntos) o al menos esto.</summary>
    public const int MinimoCobroCop = 2_000;

    /// <summary>Puntos que cubren todo el precio de Destacar o Verificar sin plan (60 y 200).</summary>
    public static int PuntosParaCubrir(int precioCop) => (int)Math.Ceiling(precioCop / (decimal)CopPorEcoPunto);

    /// <summary>
    /// Precio final: primero el descuento del plan vigente (Premium 15 %, Empresa 20 %); después, los puntos que la persona
    /// ELIGE usar (nunca se canjean solos). O cubren todo, o dejan al menos <see cref="MinimoCobroCop"/> para la pasarela.
    /// </summary>
    public static CotizacionBeneficio Cotizar(int precioBaseCop, int saldoPuntos, int descuentoPlanPorcentaje, int puntosAUsar)
    {
        var descuento = Math.Clamp(descuentoPlanPorcentaje, 0, 90);
        var conPlan = (int)Math.Round(precioBaseCop * (100 - descuento) / 100m, MidpointRounding.AwayFromZero);
        var cubrirTodo = PuntosParaCubrir(conPlan);
        var maxParcial = Math.Max(0, (conPlan - MinimoCobroCop) / CopPorEcoPunto);
        var maximos = saldoPuntos >= cubrirTodo ? cubrirTodo : Math.Min(saldoPuntos, maxParcial);
        if (puntosAUsar < 0 || puntosAUsar > Math.Max(0, saldoPuntos))
            throw new ReglaDeNegocioException("No tienes esos Eco-Puntos.");
        if (puntosAUsar > maximos || (puntosAUsar > maxParcial && puntosAUsar < cubrirTodo))
            throw new ReglaDeNegocioException(
                $"Con Eco-Puntos puedes cubrirlo todo ({cubrirTodo} puntos) o dejar al menos {MinimoCobroCop:N0} COP por pagar (hasta {maxParcial} puntos).");
        var usar = Math.Min(puntosAUsar, cubrirTodo);
        var total = Math.Max(0, conPlan - usar * CopPorEcoPunto);
        return new CotizacionBeneficio(precioBaseCop, descuento, usar, total, conPlan, maximos, cubrirTodo);
    }
}
