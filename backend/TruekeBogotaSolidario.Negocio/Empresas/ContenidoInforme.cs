using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TruekeBogotaSolidario.Datos.Entidades;
using TruekeBogotaSolidario.Datos.Repositorios;

namespace TruekeBogotaSolidario.Negocio.Empresas;

/// <summary>
/// Foto inmutable de un informe de impacto: lo que se guarda en <see cref="InformeImpacto.Contenido"/>. El PDF y el CSV se
/// generan solo desde aquí, así que siempre dan las mismas cifras y no cambian si después cambian los factores.
/// Las entregas van anonimizadas: un código por intercambio, sin personas, títulos ni coordenadas.
/// </summary>
public sealed record ContenidoInforme(
    int Formato,
    string RazonSocial,
    string Nit,
    string? Campana,
    DateOnly? CampanaInicio,
    DateOnly? CampanaFin,
    DateOnly Desde,
    DateOnly Hasta,
    DateTime EmitidoUtc,
    IReadOnlyList<string> Versiones,
    ResumenInforme Resumen,
    IReadOnlyList<FilaAgrupada> PorMaterial,
    IReadOnlyList<FilaAgrupada> PorLocalidad,
    IReadOnlyList<FilaAgrupada> PorModo,
    IReadOnlyList<FactorUsado> Factores,
    IReadOnlyList<EntregaInforme> Entregas);

/// <param name="Intercambios">Intercambios completados con doble confirmación (un trueke con varios objetos es uno).</param>
/// <param name="KgDeclarados">Kilos que declaró quien publicó (el resto es el peso típico de la categoría).</param>
/// <param name="ConEvidencia">Intercambios en que alguna de las partes adjuntó foto de la entrega.</param>
public sealed record ResumenInforme(int Intercambios, int Objetos, decimal Kg, decimal KgDeclarados, decimal KgCo2, int ConEvidencia);

public sealed record FilaAgrupada(string Nombre, int Objetos, decimal Kg, decimal KgCo2);

/// <param name="Fuente">La fuente vigente si el factor sigue en esa versión; si ya cambió, se remite al historial de la metodología.</param>
public sealed record FactorUsado(string Material, string Version, decimal KgCo2PorKg, string Fuente);

/// <param name="Codigo">Código del intercambio dentro del informe (I-0001…): varios objetos de un trueke comparten código.</param>
public sealed record EntregaInforme(string Codigo, DateOnly Fecha, string? Campana, string Localidad, string Modo, string Categoria,
    string Material, decimal Kg, string OrigenPeso, decimal FactorKgCo2PorKg, decimal KgCo2, string Version, bool Evidencia);

public static class ContenidoInformes
{
    public const int FormatoActual = 1;
    public const string UrlMetodologia = "https://github.com/IngMartinG/TRUEKE-BOGOTA-SOLIDARIO-/blob/main/docs/METODOLOGIA-IMPACTO.md";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string NombreMaterial(MaterialObjeto m) => m switch
    {
        MaterialObjeto.Textil => "Textil",
        MaterialObjeto.Electronica => "Electrónica",
        MaterialObjeto.Electrodomestico => "Electrodoméstico",
        MaterialObjeto.Madera => "Madera",
        MaterialObjeto.Papel => "Papel y cartón",
        MaterialObjeto.Plastico => "Plástico",
        MaterialObjeto.Vidrio => "Vidrio",
        MaterialObjeto.Metal => "Metal",
        _ => "Varios materiales",
    };

    public static string NombreModo(ModoTransaccion m) => m switch
    {
        ModoTransaccion.Trueke => "Trueke",
        ModoTransaccion.Compra => "Compra",
        _ => "Donación",
    };

    /// <summary>Días de Colombia [desde, hasta] → instantes UTC [desde, hasta+1).</summary>
    public static (DateTime DesdeUtc, DateTime HastaUtc) RangoUtc(DateOnly desde, DateOnly hasta)
        => (desde.ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc), hasta.AddDays(1).ToDateTime(new TimeOnly(5, 0), DateTimeKind.Utc));

    public static ContenidoInforme Construir(Empresa empresa, Campana? campana, IReadOnlyDictionary<Guid, string> nombresCampanas,
        DateOnly desde, DateOnly hasta, DateTime emitidoUtc, IReadOnlyList<RegistroParaInforme> registros,
        IReadOnlyDictionary<int, string> categorias, IReadOnlyList<FactorImpacto> factoresVigentes)
    {
        var codigos = new Dictionary<Guid, string>();
        var entregas = registros.Select(x =>
        {
            var r = x.Registro;
            if (!codigos.TryGetValue(r.TransaccionId, out var codigo))
                codigos[r.TransaccionId] = codigo = $"I-{codigos.Count + 1:0000}";
            return new EntregaInforme(codigo, Usuario.HoyEnColombia(r.FechaUtc),
                r.CampanaId is { } c && nombresCampanas.TryGetValue(c, out var nombre) ? nombre : null,
                r.Localidad, NombreModo(r.Modo), categorias.GetValueOrDefault(r.CategoriaId) ?? "Otros", NombreMaterial(r.Material), r.Kg,
                r.OrigenPeso == OrigenPeso.Declarado ? "Declarado" : "Estimado por categoría", r.FactorKgCo2PorKg, r.KgCo2, r.Version, x.ConEvidencia);
        }).ToList();

        static List<FilaAgrupada> Agrupar(IEnumerable<EntregaInforme> filas, Func<EntregaInforme, string> clave)
            => filas.GroupBy(clave).Select(g => new FilaAgrupada(g.Key, g.Count(), g.Sum(e => e.Kg), g.Sum(e => e.KgCo2)))
                .OrderByDescending(f => f.Kg).ThenBy(f => f.Nombre, StringComparer.Ordinal).ToList();

        var vigentes = factoresVigentes.ToDictionary(f => NombreMaterial(f.Material));
        var factores = entregas.Select(e => (e.Material, e.Version, e.FactorKgCo2PorKg)).Distinct()
            .OrderBy(f => f.Material, StringComparer.Ordinal).ThenBy(f => f.Version, StringComparer.Ordinal)
            .Select(f => new FactorUsado(f.Material, f.Version, f.FactorKgCo2PorKg,
                vigentes.TryGetValue(f.Material, out var v) && v.Version == f.Version && v.KgCo2PorKg == f.FactorKgCo2PorKg
                    ? v.Fuente : "Factor de una versión anterior: ver el historial de la metodología."))
            .ToList();

        var resumen = new ResumenInforme(codigos.Count, entregas.Count, entregas.Sum(e => e.Kg),
            entregas.Where(e => e.OrigenPeso == "Declarado").Sum(e => e.Kg), entregas.Sum(e => e.KgCo2),
            entregas.Where(e => e.Evidencia).Select(e => e.Codigo).Distinct().Count());

        return new ContenidoInforme(FormatoActual, empresa.RazonSocial, empresa.Nit, campana?.Nombre, campana?.Inicio, campana?.Fin,
            desde, hasta, emitidoUtc, entregas.Select(e => e.Version).Distinct().OrderBy(v => v, StringComparer.Ordinal).ToList(), resumen,
            Agrupar(entregas, e => e.Material), Agrupar(entregas, e => e.Localidad), Agrupar(entregas, e => e.Modo), factores, entregas);
    }

    public static string Serializar(ContenidoInforme c) => JsonSerializer.Serialize(c, Json);

    public static ContenidoInforme Leer(string json)
        => JsonSerializer.Deserialize<ContenidoInforme>(json, Json) ?? throw new InvalidOperationException("Informe sin contenido.");

    /// <summary>SHA-256 (hex minúscula) del contenido guardado.</summary>
    public static string Huella(string json) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
}
