using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TruekeBogotaSolidario.Negocio.Empresas;

/// <summary>
/// PDF del informe de impacto (QuestPDF, licencia Community): portada, resumen, metodología con su versión, resultados,
/// trazabilidad anonimizada y anexos. Se genera siempre desde la foto guardada (<see cref="ContenidoInforme"/>).
/// </summary>
public static class InformePdf
{
    private const string Verde = "#1f7a4d";
    private const string VerdeOscuro = "#123d27";
    private const string VerdeClaro = "#eefaf3";
    private const string Gris = "#5b6660";
    private const string Linea = "#d6e2db";

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    static InformePdf() => QuestPDF.Settings.License = LicenseType.Community;

    public static string Numero(decimal valor, int decimales = 2) => valor.ToString("N" + decimales, Co);
    private static string Fecha(DateOnly d) => d.ToString("d 'de' MMMM 'de' yyyy", Co);

    /// <summary>La frase del resumen con las cifras principales (la prueba de PDF y CSV la busca en el texto del PDF).</summary>
    public static string FraseResumen(ResumenInforme r)
        => $"En el periodo se completaron {r.Intercambios} intercambios con {r.Objetos} objetos entregados: {Numero(r.Kg)} kg reutilizados " +
           $"y ≈ {Numero(r.KgCo2)} kg de CO₂e evitados.";

    public static byte[] Generar(ContenidoInforme c, Guid informeId, string huella)
    {
        var alcance = c.Campana is null ? "Todas las campañas de la empresa" : $"Campaña «{c.Campana}»";
        var periodo = $"Del {Fecha(c.Desde)} al {Fecha(c.Hasta)}";
        var versiones = string.Join(", ", c.Versiones.DefaultIfEmpty("sin registros"));

        return Document.Create(doc =>
        {
            doc.Page(p =>
            {
                Base(p, informeId);
                p.Content().PaddingTop(3, Unit.Centimetre).Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text("Trueke Bogotá Solidario").FontSize(14).Bold().FontColor(Verde);
                    col.Item().PaddingTop(30).Text("Informe de impacto").FontSize(32).Bold().FontColor(VerdeOscuro);
                    col.Item().Text(c.RazonSocial).FontSize(20);
                    col.Item().Text($"NIT {c.Nit}").FontSize(12).FontColor(Gris);
                    col.Item().PaddingTop(20).LineHorizontal(1).LineColor(Linea);
                    col.Item().Text(alcance).FontSize(12);
                    if (c.CampanaInicio is { } ini && c.CampanaFin is { } fin)
                        col.Item().Text($"Vigencia de la campaña: del {Fecha(ini)} al {Fecha(fin)}").FontSize(10).FontColor(Gris);
                    col.Item().Text(periodo).FontSize(12);
                    col.Item().Text($"Metodología de impacto: versión {versiones}").FontSize(12);
                    col.Item().Text($"Emitido el {Fecha(DateOnly.FromDateTime(c.EmitidoUtc.AddHours(-5)))} por la administración de Trueke Bogotá Solidario")
                        .FontSize(10).FontColor(Gris);
                    col.Item().PaddingTop(40).Background(VerdeClaro).Padding(12).Column(r =>
                    {
                        r.Item().Text($"{Numero(c.Resumen.Kg)} kg reutilizados").FontSize(22).Bold().FontColor(VerdeOscuro);
                        r.Item().Text($"≈ {Numero(c.Resumen.KgCo2)} kg de CO₂e evitados (estimación)").FontSize(14).FontColor(Verde);
                        r.Item().Text($"{c.Resumen.Objetos} objetos en {c.Resumen.Intercambios} intercambios con doble confirmación").FontSize(11);
                    });
                });
            });

            doc.Page(p =>
            {
                Base(p, informeId);
                p.Content().Column(col =>
                {
                    col.Spacing(8);
                    Titulo(col, "1. Resumen");
                    col.Item().Text(FraseResumen(c.Resumen));
                    col.Item().Text($"{alcance}. {periodo}. De los kilos, {Numero(c.Resumen.KgDeclarados)} kg los declaró quien publicó y el resto es el " +
                        $"peso típico de su categoría. En {c.Resumen.ConEvidencia} de los {c.Resumen.Intercambios} intercambios alguna de las partes " +
                        "adjuntó una foto de la entrega.");

                    Titulo(col, "2. Metodología");
                    col.Item().Text($"Versión aplicada: {versiones}. Cada registro guarda la versión y el factor con que se calculó: este informe no cambia " +
                        "aunque después se actualicen los factores.");
                    Punto(col, "Solo cuenta lo confirmado: un objeto suma cuando las dos partes confirman la entrega. Los intercambios que se cierran " +
                        "con una sola confirmación no suman.");
                    Punto(col, "Kilos primero: el peso declarado al publicar o, si falta, el típico de la categoría.");
                    Punto(col, "CO₂e evitado = kilos × factor del material (kg de CO₂e por kg reutilizado). Factores conservadores (anexo A).");
                    Punto(col, "Cada objeto deja un registro inmutable con su peso, material, factor, versión, localidad y fecha.");
                    col.Item().PaddingTop(4).Background(VerdeClaro).Padding(8).Text(
                        "Los kilogramos reutilizados corresponden a objetos cuya entrega confirmaron ambas partes en Trueke Bogotá Solidario. " +
                        $"La estimación de CO₂ usa la metodología interna versión {versiones}, con factores conservadores. Los valores son " +
                        "estimaciones y no constituyen una verificación de tercera parte.").Italic();
                    col.Item().Text(t =>
                    {
                        t.Span("Metodología completa: ");
                        t.Hyperlink("docs/METODOLOGIA-IMPACTO.md", ContenidoInformes.UrlMetodologia).FontColor(Verde).Underline();
                    });

                    Titulo(col, "3. Resultados");
                    Subtitulo(col, "Por material");
                    TablaAgrupada(col, "Material", c.PorMaterial);
                    Subtitulo(col, "Por localidad");
                    TablaAgrupada(col, "Localidad", c.PorLocalidad);
                    Subtitulo(col, "Por modo");
                    TablaAgrupada(col, "Modo", c.PorModo);
                });
            });

            doc.Page(p =>
            {
                Base(p, informeId, horizontal: true);
                p.Content().Column(col =>
                {
                    col.Spacing(8);
                    Titulo(col, "4. Trazabilidad de entregas");
                    col.Item().Text("Una fila por objeto entregado. Las personas no se identifican: el código agrupa los objetos de un mismo intercambio. " +
                        "El archivo CSV del informe contiene exactamente estas filas.").FontColor(Gris);
                    if (c.Entregas.Count == 0) { col.Item().Text("No hubo entregas confirmadas en el periodo."); return; }
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(d =>
                        {
                            d.ConstantColumn(52); d.ConstantColumn(62); d.RelativeColumn(2); d.RelativeColumn(2); d.ConstantColumn(56);
                            d.RelativeColumn(2); d.RelativeColumn(2); d.ConstantColumn(46); d.RelativeColumn(2); d.ConstantColumn(42);
                            d.ConstantColumn(52); d.ConstantColumn(40); d.ConstantColumn(44);
                        });
                        t.Header(h =>
                        {
                            foreach (var titulo in new[] { "Código", "Fecha", "Campaña", "Localidad", "Modo", "Categoría", "Material", "Kg",
                                         "Origen del peso", "Factor", "kg CO₂e", "Versión", "Foto" })
                                Encabezado(h.Cell(), titulo);
                        });
                        foreach (var e in c.Entregas)
                        {
                            Celda(t.Cell(), e.Codigo); Celda(t.Cell(), e.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                            Celda(t.Cell(), e.Campana ?? "—"); Celda(t.Cell(), e.Localidad); Celda(t.Cell(), e.Modo); Celda(t.Cell(), e.Categoria);
                            Celda(t.Cell(), e.Material); Celda(t.Cell(), Numero(e.Kg), derecha: true); Celda(t.Cell(), e.OrigenPeso);
                            Celda(t.Cell(), Numero(e.FactorKgCo2PorKg, 3), derecha: true); Celda(t.Cell(), Numero(e.KgCo2), derecha: true);
                            Celda(t.Cell(), e.Version); Celda(t.Cell(), e.Evidencia ? "Sí" : "No");
                        }
                    });
                });
            });

            doc.Page(p =>
            {
                Base(p, informeId);
                p.Content().Column(col =>
                {
                    col.Spacing(8);
                    Titulo(col, "5. Anexos");
                    Subtitulo(col, "A. Factores usados (kg de CO₂e por kg reutilizado)");
                    if (c.Factores.Count == 0) col.Item().Text("Sin registros en el periodo.");
                    else col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(d => { d.RelativeColumn(2); d.ConstantColumn(50); d.ConstantColumn(50); d.RelativeColumn(5); });
                        t.Header(h => { Encabezado(h.Cell(), "Material"); Encabezado(h.Cell(), "Versión"); Encabezado(h.Cell(), "Factor"); Encabezado(h.Cell(), "Fuente"); });
                        foreach (var f in c.Factores)
                        {
                            Celda(t.Cell(), f.Material); Celda(t.Cell(), f.Version);
                            Celda(t.Cell(), Numero(f.KgCo2PorKg, 3), derecha: true); Celda(t.Cell(), f.Fuente);
                        }
                    });
                    Subtitulo(col, "B. Definiciones");
                    Punto(col, "Intercambio: trueke, compra o donación en que las dos partes confirmaron la entrega.");
                    Punto(col, "Peso declarado: el que indicó quien publicó. Estimado por categoría: el peso típico de su categoría.");
                    Punto(col, "Campaña: periodo en que la empresa ligó sus publicaciones para medir su impacto.");
                    Punto(col, "Foto: alguna de las partes adjuntó una foto al confirmar la entrega (la foto no se incluye por privacidad).");
                    Subtitulo(col, "C. Qué no se afirma");
                    Punto(col, "Este informe no es una certificación ni una verificación de tercera parte.");
                    Punto(col, "No se cuentan objetos sin la doble confirmación de la entrega.");
                    Punto(col, "Las cifras de CO₂e son estimaciones, no mediciones.");
                    Subtitulo(col, "D. Integridad");
                    col.Item().Text($"Informe {informeId}. Los datos de este informe se guardaron al emitirlo y no se recalculan. Huella SHA-256 de " +
                        "esos datos:");
                    col.Item().Background(VerdeClaro).Padding(6).Text(huella).FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static void Base(PageDescriptor p, Guid informeId, bool horizontal = false)
    {
        p.Size(horizontal ? PageSizes.A4.Landscape() : PageSizes.A4);
        p.Margin(horizontal ? 1.2f : 2f, Unit.Centimetre);
        p.DefaultTextStyle(x => x.FontSize(horizontal ? 8 : 10).FontColor("#1d2421"));
        p.Footer().Row(r =>
        {
            r.RelativeItem().Text($"Trueke Bogotá Solidario · Informe {informeId.ToString()[..8]}").FontSize(8).FontColor(Gris);
            r.RelativeItem().AlignRight().Text(t =>
            {
                t.DefaultTextStyle(x => x.FontSize(8).FontColor(Gris));
                t.Span("Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        });
    }

    private static void Titulo(ColumnDescriptor col, string texto)
        => col.Item().PaddingTop(10).Text(texto).FontSize(15).Bold().FontColor(VerdeOscuro);

    private static void Subtitulo(ColumnDescriptor col, string texto)
        => col.Item().PaddingTop(4).Text(texto).FontSize(11).Bold().FontColor(Verde);

    private static void Punto(ColumnDescriptor col, string texto)
        => col.Item().Row(r => { r.ConstantItem(12).Text("•").FontColor(Verde); r.RelativeItem().Text(texto); });

    private static void Encabezado(IContainer celda, string texto)
        => celda.Background(VerdeClaro).BorderBottom(1).BorderColor(Verde).Padding(3).Text(texto).Bold().FontColor(VerdeOscuro);

    private static void Celda(IContainer celda, string texto, bool derecha = false)
    {
        var c = celda.BorderBottom(0.5f).BorderColor(Linea).Padding(3);
        (derecha ? c.AlignRight() : c).Text(texto);
    }

    private static void TablaAgrupada(ColumnDescriptor col, string titulo, IReadOnlyList<FilaAgrupada> filas)
    {
        if (filas.Count == 0) { col.Item().Text("Sin registros en el periodo.").FontColor(Gris); return; }
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(d => { d.RelativeColumn(3); d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn(); });
            t.Header(h => { Encabezado(h.Cell(), titulo); Encabezado(h.Cell(), "Objetos"); Encabezado(h.Cell(), "Kg"); Encabezado(h.Cell(), "kg CO₂e"); });
            foreach (var f in filas)
            {
                Celda(t.Cell(), f.Nombre); Celda(t.Cell(), f.Objetos.ToString(Co), derecha: true);
                Celda(t.Cell(), Numero(f.Kg), derecha: true); Celda(t.Cell(), Numero(f.KgCo2), derecha: true);
            }
        });
    }
}
