using TruekeBogotaSolidario.Datos.Common;
using TruekeBogotaSolidario.Datos.Entidades;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Avisos de Eco-Puntos que dependen de comparar el antes y el después: el nivel alcanzado y los bonos de una sola vez
/// (perfil completo y primera publicación). Se observa a cada persona ANTES de sumarle puntos y se avisa DESPUÉS de guardar
/// (si el guardado falla, nadie recibe un aviso falso). Lo usan todos los servicios que acreditan puntos que cuentan para el nivel.
/// </summary>
public sealed class AvisosEcoPuntos
{
    private readonly Dictionary<Guid, (Usuario Usuario, NivelEcoPuntos Nivel, bool Perfil, bool PrimeraPublicacion)> _antes = new();

    /// <summary>Guarda el estado actual de la persona. Llamarlo varias veces no cambia lo que se observó la primera.</summary>
    public void Observar(Usuario u)
        => _antes.TryAdd(u.Id, (u, u.Nivel, u.BonoPerfilCompletoOtorgado, u.BonoPrimeraPublicacionOtorgado));

    public async Task EnviarAsync(INotificador notificador)
    {
        foreach (var (u, nivel, perfil, primera) in _antes.Values)
        {
            if (!perfil && u.BonoPerfilCompletoOtorgado)
                await notificador.NotificarAsync(u.Id, TiposNotificacion.EcoPuntosGanados,
                    $"¡Completaste tu perfil! Ganaste {PoliticaEcoPuntos.PuntosPerfilCompleto} Eco-Puntos.");
            if (!primera && u.BonoPrimeraPublicacionOtorgado)
            {
                var publicacion = u.MovimientosEcoPuntos.LastOrDefault(m => m.Concepto == ConceptoEcoPuntos.PrimeraPublicacion)?.ReferenciaId;
                await notificador.NotificarAsync(u.Id, TiposNotificacion.EcoPuntosGanados,
                    $"¡Tu primera publicación ya está ayudando a la comunidad! Ganaste {PoliticaEcoPuntos.PuntosPrimeraPublicacion} Eco-Puntos.",
                    publicacion);
            }
            if (u.Nivel > nivel)
                await notificador.NotificarAsync(u.Id, TiposNotificacion.NivelAlcanzado, MensajeNivel(u.Nivel));
        }
    }

    public static string MensajeNivel(NivelEcoPuntos nivel) => nivel switch
    {
        NivelEcoPuntos.Brote => "¡Subiste a Brote 🌿! Tus intercambios ya están dando fruto.",
        NivelEcoPuntos.Arbol => "¡Subiste a Árbol 🌳! Eres parte de las raíces de la comunidad.",
        NivelEcoPuntos.Bosque => "¡Subiste a Bosque 🌲! Tu aporte ya se nota en toda la comunidad.",
        NivelEcoPuntos.Guardian => "¡Llegaste a Guardián 🛡️, el nivel más alto! Gracias por cuidar la economía circular.",
        _ => "¡Subiste de nivel!"
    };
}
