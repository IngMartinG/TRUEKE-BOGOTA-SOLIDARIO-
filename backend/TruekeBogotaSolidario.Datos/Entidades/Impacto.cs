using TruekeBogotaSolidario.Datos.Common;

namespace TruekeBogotaSolidario.Datos.Entidades;

/// <summary>Material principal del objeto: define el factor de CO₂ por kilo (metodología de impacto 2.0.0).</summary>
public enum MaterialObjeto { Textil = 1, Electronica = 2, Electrodomestico = 3, Madera = 4, Papel = 5, Plastico = 6, Vidrio = 7, Metal = 8, Mixto = 9 }

/// <summary>De dónde salió el peso de un registro de impacto: lo declaró quien publicó o es el típico de su categoría.</summary>
public enum OrigenPeso { Declarado = 1, EstimadoPorCategoria = 2 }

/// <summary>
/// kg de CO₂e que se dejan de emitir por cada kg reutilizado de un material (en lugar de fabricar uno nuevo). Lo edita el
/// SuperUsuario con su fuente y una versión nueva; los registros ya hechos guardan el factor con que se calcularon.
/// </summary>
public class FactorImpacto
{
    public const int LongitudMaximaFuente = 300;
    public const int LongitudMaximaVersion = 20;

    private FactorImpacto() { Fuente = ""; Version = ""; } // EF Core

    public FactorImpacto(MaterialObjeto material, decimal kgCo2PorKg, string fuente, string version, DateTime ahoraUtc)
    {
        Material = material;
        Fuente = ""; Version = "";
        Actualizar(kgCo2PorKg, fuente, version, null, ahoraUtc);
    }

    public MaterialObjeto Material { get; private set; }
    public decimal KgCo2PorKg { get; private set; }
    public string Fuente { get; private set; }
    /// <summary>Versión de la metodología con que se fijó este factor (por ejemplo "2.0.0").</summary>
    public string Version { get; private set; }
    public DateTime ActualizadoUtc { get; private set; }
    public Guid? ActualizadoPorId { get; private set; }

    public static bool VersionValida(string? v)
        => v is { Length: > 0 and <= LongitudMaximaVersion } && System.Text.RegularExpressions.Regex.IsMatch(v, @"^\d{1,3}\.\d{1,3}\.\d{1,3}$");

    public void Actualizar(decimal kgCo2PorKg, string fuente, string version, Guid? actorId, DateTime ahoraUtc)
    {
        var texto = (fuente ?? "").Trim();
        var ver = (version ?? "").Trim();
        if (kgCo2PorKg is < 0 or > 100) throw new ReglaDeNegocioException("El factor debe estar entre 0 y 100 kg de CO₂ por kg.");
        if (texto.Length is < 3 or > LongitudMaximaFuente) throw new ReglaDeNegocioException($"Indica la fuente del factor (entre 3 y {LongitudMaximaFuente} caracteres).");
        if (!VersionValida(ver)) throw new ReglaDeNegocioException("La versión debe tener el formato 2.1.0.");
        if (Version.Length > 0 && ver == Version) throw new ReglaDeNegocioException("Un cambio de factor exige una versión nueva de la metodología.");
        KgCo2PorKg = Math.Round(kgCo2PorKg, 3);
        Fuente = texto;
        Version = ver;
        ActualizadoPorId = actorId;
        ActualizadoUtc = ahoraUtc;
    }
}

/// <summary>
/// Un objeto que cambió de manos en un intercambio que ambas partes confirmaron. Inmutable: guarda el peso, el material, el
/// factor y la versión con que se calculó, para que el resultado sea reproducible aunque los factores cambien después.
/// </summary>
public class RegistroImpacto
{
    private RegistroImpacto() { Version = ""; Localidad = ""; MunicipioCodigo = ""; } // EF Core

    public RegistroImpacto(Guid transaccionId, Publicacion objeto, ModoTransaccion modo, Guid oferenteId, Guid receptorId,
        decimal kg, OrigenPeso origen, MaterialObjeto material, FactorImpacto factor, DateTime fechaUtc)
    {
        Id = Guid.NewGuid();
        TransaccionId = transaccionId;
        PublicacionId = objeto.Id;
        CategoriaId = objeto.CategoriaId;
        Modo = modo;
        OferenteId = oferenteId;
        ReceptorId = receptorId;
        Kg = Math.Round(kg, 2);
        OrigenPeso = origen;
        Material = material;
        FactorKgCo2PorKg = factor.KgCo2PorKg;
        KgCo2 = Math.Round(Kg * factor.KgCo2PorKg, 2);
        Version = factor.Version;
        MunicipioCodigo = objeto.MunicipioCodigo;
        Localidad = objeto.Localidad;
        CampanaId = objeto.CampanaId;
        FechaUtc = fechaUtc;
    }

    public Guid Id { get; private set; }
    public Guid TransaccionId { get; private set; }
    public Guid PublicacionId { get; private set; }
    public int CategoriaId { get; private set; }
    /// <summary>Modo en que se cerró el intercambio (una venta que terminó en trueke cuenta como trueke).</summary>
    public ModoTransaccion Modo { get; private set; }
    /// <summary>Quien entregó este objeto y quien lo recibió (en un trueke con objetos, cada objeto va en un sentido).</summary>
    public Guid OferenteId { get; private set; }
    public Guid ReceptorId { get; private set; }
    public decimal Kg { get; private set; }
    public OrigenPeso OrigenPeso { get; private set; }
    public MaterialObjeto Material { get; private set; }
    public decimal FactorKgCo2PorKg { get; private set; }
    public decimal KgCo2 { get; private set; }
    /// <summary>Versión de la metodología (la del factor usado).</summary>
    public string Version { get; private set; }
    public string MunicipioCodigo { get; private set; }
    public string Localidad { get; private set; }
    /// <summary>Campaña de empresa con que se publicó el objeto (entra en sus informes).</summary>
    public Guid? CampanaId { get; private set; }
    public DateTime FechaUtc { get; private set; }
}

/// <summary>
/// Meta mensual de kilos reutilizados en una localidad (por ejemplo "Bosa: 1 tonelada este mes"). La fija el SuperUsuario y se
/// repite cada mes; el avance se calcula con los registros de impacto del mes en curso.
/// </summary>
public class MetaComunitaria
{
    public const decimal KgMinimo = 10m;
    public const decimal KgMaximo = 100_000m;

    private MetaComunitaria() { MunicipioCodigo = ""; Localidad = ""; } // EF Core

    public MetaComunitaria(string municipioCodigo, string localidad, decimal metaKg, Guid creadaPorId, DateTime ahoraUtc)
    {
        Id = Guid.NewGuid();
        MunicipioCodigo = municipioCodigo;
        Localidad = localidad;
        CreadaPorId = creadaPorId;
        FechaUtc = ahoraUtc;
        CambiarMeta(metaKg);
    }

    public Guid Id { get; private set; }
    public string MunicipioCodigo { get; private set; }
    public string Localidad { get; private set; }
    public decimal MetaKg { get; private set; }
    public Guid CreadaPorId { get; private set; }
    public DateTime FechaUtc { get; private set; }

    public void CambiarMeta(decimal metaKg)
    {
        if (metaKg < KgMinimo || metaKg > KgMaximo)
            throw new ReglaDeNegocioException($"La meta debe estar entre {KgMinimo:0} y {KgMaximo:0} kg.");
        MetaKg = Math.Round(metaKg, 0);
    }
}
