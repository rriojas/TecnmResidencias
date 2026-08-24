using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TecNM.Residency.Common;

public class PresentationLetterData
{
    public string StudentFullName { get; set; } = string.Empty;
    public string ControlNumber { get; set; } = string.Empty;
    public string CareerName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = "A QUIEN CORRESPONDA";
    public string FolioNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
}

public static class PresentationLetterPdfService
{
    private static readonly Color TecnmBlue = Color.FromHex("#1B396A");
    private static readonly Color TecnmGold = Color.FromHex("#C5A059");

    public static byte[] GeneratePresentationLetterPdf(PresentationLetterData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(45);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken4).FontFamily("Arial"));

                // Header institucional
                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("TECNOLÓGICO NACIONAL DE MÉXICO")
                                .FontSize(13).Bold().FontColor(TecnmBlue).AlignCenter();
                            col.Item().Text("INSTITUTO TECNOLÓGICO DE MONCLOVA")
                                .FontSize(11).SemiBold().FontColor(TecnmBlue).AlignCenter();
                            col.Item().Text("DEPARTAMENTO DE GESTIÓN TECNOLÓGICA Y VINCULACIÓN")
                                .FontSize(9).FontColor(TecnmGold).Bold().AlignCenter();
                        });
                    });

                    header.Item().PaddingTop(12).LineHorizontal(1.5f).LineColor(TecnmGold);
                });

                // Contenido oficial de la Carta de Presentación
                page.Content().Column(content =>
                {
                    content.Spacing(14);

                    content.Item().PaddingTop(10).AlignRight().Text(text =>
                    {
                        text.Span("Asunto: ").Bold();
                        text.Span("Carta de Presentación para Residencia Profesional\n");
                        text.Span($"Folio: {data.FolioNumber}\n").FontSize(9).FontColor(TecnmBlue);
                        text.Span($"Monclova, Coahuila; a {data.IssueDate:dd} de {GetMonthName(data.IssueDate.Month)} de {data.IssueDate:yyyy}.").FontSize(10);
                    });

                    content.Item().PaddingTop(10).Column(col =>
                    {
                        col.Item().Text(data.CompanyName.ToUpper()).FontSize(11).Bold();
                        col.Item().Text("PRESENTE.").FontSize(11).Bold();
                    });

                    content.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span("Por medio de la presente, el ");
                        text.Span("Instituto Tecnológico de Monclova").Bold().FontColor(TecnmBlue);
                        text.Span(" presenta formalmente al C. ");
                        text.Span(data.StudentFullName.ToUpper()).Bold();
                        text.Span(", con número de control ");
                        text.Span(data.ControlNumber).Bold();
                        text.Span(", alumno(a) inscrito(a) en el programa educativo de ");
                        text.Span(data.CareerName.ToUpper()).Bold();
                        text.Span(".");
                    });

                    content.Item().Text(text =>
                    {
                        text.Span("Quien ha cubierto los créditos académicos requeridos conforme a los lineamientos vigentes del Tecnológico Nacional de México, encontrándose en aptitud para desarrollar su ");
                        text.Span("Residencia Profesional").Bold();
                        text.Span(" con una duración obligatoria de 500 horas lectivas en su prestigiada institución o empresa.");
                    });

                    content.Item().Text(text =>
                    {
                        text.Span("Agradecemos las facilidades brindadas al estudiante para el desarrollo de su proyecto, el cual contribuirá significativamente al fortalecimiento de sus competencias profesionales y al desarrollo tecnológico de la región.");
                    });

                    content.Item().PaddingTop(10).Text("Sin otro particular por el momento, aprovecho la ocasión para enviarle un cordial y respetuoso saludo.");

                    // Atentamente y Firmas
                    content.Item().PaddingTop(30).AlignCenter().Column(col =>
                    {
                        col.Item().Text("ATENTAMENTE").FontSize(10).Bold().FontColor(TecnmBlue).AlignCenter();
                        col.Item().Text("\"Excelencia en Educación Tecnológica\"").FontSize(9).Italic().FontColor(TecnmGold).AlignCenter();
                        col.Item().PaddingTop(45).Text("_________________________________________").AlignCenter();
                        col.Item().Text("ING. OFICINA DE VINCULACIÓN Y GESTIÓN").FontSize(10).Bold().AlignCenter();
                        col.Item().Text("Instituto Tecnológico de Monclova").FontSize(9).FontColor(Colors.Grey.Darken2).AlignCenter();
                    });
                });

                // Pie de página
                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static string GetMonthName(int month)
    {
        string[] months = { "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
        return month >= 1 && month <= 12 ? months[month - 1] : string.Empty;
    }
}
