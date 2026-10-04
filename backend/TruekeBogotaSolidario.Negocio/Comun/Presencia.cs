using System.Collections.Concurrent;

namespace TruekeBogotaSolidario.Negocio.Comun;

/// <summary>
/// Quién tiene la app abierta ("en línea" en el chat). Se cuenta por conexión de SignalR: un usuario con dos pestañas
/// sigue en línea hasta cerrar la última. Solo se muestra a quienes comparten una conversación con él y nunca hay "última vez".
/// </summary>
public interface IPresencia
{
    /// <summary>Devuelve true si es la primera conexión del usuario (acaba de entrar en línea).</summary>
    Task<bool> ConectarAsync(Guid usuarioId, string conexionId);
    /// <summary>Devuelve true si era su última conexión (acaba de salir).</summary>
    Task<bool> DesconectarAsync(Guid usuarioId, string conexionId);
    Task<IReadOnlySet<Guid>> EnLineaAsync(IReadOnlyCollection<Guid> usuarioIds);
}

/// <summary>Presencia de una sola instancia (desarrollo, pruebas o sin Redis).</summary>
public sealed class PresenciaEnMemoria : IPresencia
{
    private readonly ConcurrentDictionary<Guid, HashSet<string>> _conexiones = new();

    public Task<bool> ConectarAsync(Guid usuarioId, string conexionId)
    {
        var conjunto = _conexiones.GetOrAdd(usuarioId, _ => new HashSet<string>());
        lock (conjunto)
        {
            var primera = conjunto.Count == 0;
            conjunto.Add(conexionId);
            return Task.FromResult(primera);
        }
    }

    public Task<bool> DesconectarAsync(Guid usuarioId, string conexionId)
    {
        if (!_conexiones.TryGetValue(usuarioId, out var conjunto)) return Task.FromResult(false);
        lock (conjunto)
        {
            var ultima = conjunto.Remove(conexionId) && conjunto.Count == 0;
            return Task.FromResult(ultima);
        }
    }

    public Task<IReadOnlySet<Guid>> EnLineaAsync(IReadOnlyCollection<Guid> usuarioIds)
    {
        var resultado = new HashSet<Guid>();
        foreach (var id in usuarioIds)
            if (_conexiones.TryGetValue(id, out var conjunto))
                lock (conjunto)
                    if (conjunto.Count > 0) resultado.Add(id);
        return Task.FromResult<IReadOnlySet<Guid>>(resultado);
    }
}
