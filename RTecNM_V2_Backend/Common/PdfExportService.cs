using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TecNM.Residency.Common;

public class PdfTableDefinition
{
    public string Title { get; set; } = string.Empty;
    public List<string> Headers { get; set; } = new();
    public List<List<string>> Rows { get; set; } = new();
}

public static class PdfExportService
{
    private static readonly Color TecnmBlue = Color.FromHex("#1B396A");
    private static readonly Color TecnmGold = Color.FromHex("#C5A059");

    public static byte[] GenerateTablePdf(PdfTableDefinition def)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken3));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Text(def.Title).FontSize(13).SemiBold().FontColor(TecnmBlue);
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (var i = 0; i < def.Headers.Count; i++)
                            cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var h in def.Headers)
                            header.Cell()
                                .Background(TecnmBlue)
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .Padding(4)
                                .Text(h)
                                .FontColor(Colors.White)
                                .SemiBold()
                                .FontSize(9);
                    });

                    foreach (var row in def.Rows)
                    {
                        foreach (var cell in row)
                            table.Cell()
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(cell ?? string.Empty);
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                    });

                    column.Item().PaddingTop(2).AlignRight().Text(x =>
                    {
                        x.Span("TecNM Campus Monclova - Sistema de Residencias Profesionales | Generado: ").FontSize(7).FontColor(Colors.Grey.Darken1);
                        x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).SemiBold().FontColor(TecnmGold);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}
