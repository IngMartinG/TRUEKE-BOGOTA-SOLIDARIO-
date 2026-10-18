using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>
/// Algo que la persona busca ("Lo que busco"). Si es público aparece en su perfil y sirve para encontrar truekes en los dos
/// sentidos: quien tiene lo que buscas y busca lo que tienes. Con avisos activos, una tarea periódica le cuenta cuando alguien
/// publica algo que coincide (desde <see cref="RevisadoHastaUtc"/>).
/// </summary>
public class Deseo
{
    public const int MaxPorUsuario = 10;
    public const int LongitudMaxima = 60;

    private Deseo() { Texto = ""; } // EF Core

    public Deseo(Guid usuarioId, string texto, int? categoriaId, bool publico, bool avisos, DateTime ahoraUtc)
    {
        Texto = "";
        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Aplicar(texto, categoriaId);
        Publico = publico;
        AvisosActivos = avisos;
        FechaUtc = ahoraUtc;
        RevisadoHastaUtc = ahoraUtc; // solo avisa de lo que se publique DESPUÉS de crearlo
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string Texto { get; private set; }
    public int? CategoriaId { get; private set; }
    /// <summary>Se muestra en el perfil y cuenta para las coincidencias de otras personas.</summary>
    public bool Publico { get; private set; }
    public bool AvisosActivos { get; private set; }
    public DateTime FechaUtc { get; private set; }
    /// <summary>Hasta cuándo ya se revisaron publicaciones nuevas (o se avisó).</summary>
    public DateTime RevisadoHastaUtc { get; private set; }
    /// <summary>Oculto por moderación: deja de verse en el perfil y en las coincidencias de otras personas.</summary>
    public bool EstaOculto { get; private set; }
    public string? MotivoOcultamiento { get; private set; }

    /// <summary>Visible para otras personas.</summary>
    public bool EsVisible => Publico && !EstaOculto;

    private void Aplicar(string texto, int? categoriaId)
    {
        texto = (texto ?? "").Trim();
        if (texto.Length is < 3 or > LongitudMaxima)
            throw new ReglaDeNegocioException($"Cuenta lo que buscas en 3 a {LongitudMaxima} caracteres.");
        if (!TextoSeguro.EsSeguro(texto)) throw new ReglaDeNegocioException("El texto contiene caracteres no permitidos.");
        if (Common.TextoBusqueda.Terminos(texto).Count == 0)
            throw new ReglaDeNegocioException("Usa al menos una palabra que describa lo que buscas (por ejemplo: «bicicleta»).");
        Texto = texto;
        CategoriaId = categoriaId;
    }

    public void Editar(string texto, int? categoriaId, bool publico, bool avisos, DateTime ahoraUtc)
    {
        if (EstaOculto) throw new ReglaDeNegocioException("No se puede editar un deseo oculto por moderación. Puedes borrarlo y crear otro.");
        var cambioBusqueda = !string.Equals(Texto, (texto ?? "").Trim(), StringComparison.Ordinal) || CategoriaId != categoriaId;
        Aplicar(texto!, categoriaId);
        Publico = publico;
        // Con otra búsqueda, o al reactivar los avisos, no se avisa de lo que ya estaba publicado.
        if (cambioBusqueda || (avisos && !AvisosActivos)) RevisadoHastaUtc = ahoraUtc;
        AvisosActivos = avisos;
    }

    public void MarcarRevisado(DateTime hastaUtc) => RevisadoHastaUtc = hastaUtc;

    public void Ocultar(string motivo)
    {
        EstaOculto = true;
        MotivoOcultamiento = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }

    public void Mostrar()
    {
        EstaOculto = false;
        MotivoOcultamiento = null;
    }

    /// <summary>Misma regla que el buscador del catálogo: cada término al inicio de alguna palabra y, si la hay, la misma categoría.</summary>
    public bool CoincideCon(Publicacion p)
        => (CategoriaId is null || p.CategoriaId == CategoriaId)
           && Common.TextoBusqueda.Terminos(Texto).All(t => p.TextoBusqueda.Contains(" " + t, StringComparison.Ordinal));
}
