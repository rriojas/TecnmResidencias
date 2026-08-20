using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Common;

public static class ProjectPdfService
{
    private static readonly Color TecnmBlue = Color.FromHex("#1B396A");
    private static readonly Color TecnmGold = Color.FromHex("#C5A059");

    public static byte[] GenerateProjectPdf(ProjectPdfData data)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.ConstantItem(70).Text(string.Empty);
                        row.RelativeItem().AlignCenter().Column(col =>
                        {
                            col.Item().Text("Tecnológico Nacional de México")
                                .FontSize(14).Bold().FontColor(TecnmBlue).AlignCenter();
                            col.Item().Text("Instituto Tecnológico de Monclova")
                                .FontSize(11).SemiBold().FontColor(TecnmBlue).AlignCenter();
                        });
                        row.ConstantItem(70).Text(string.Empty);
                    });

                    header.Item().PaddingVertical(8).AlignCenter().Text(
                        "Solicitud de Anteproyecto de Residencia Profesional")
                        .FontSize(13).Bold().FontColor(TecnmBlue);

                    header.Item().PaddingBottom(8).LineHorizontal(1.5f).LineColor(TecnmGold);
                });

                page.Content().Column(content =>
                {
                    content.Spacing(10);

                    content.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Datos del Estudiante").FontSize(11).Bold().FontColor(TecnmBlue);
                    });
                    content.Item().Field("Estudiante", data.StudentName);
                    content.Item().Field("Empresa Receptora", data.CompanyName);
                    if (!string.IsNullOrWhiteSpace(data.CompanyRfc))
                        content.Item().Field("RFC de la Empresa", data.CompanyRfc);
                    if (!string.IsNullOrWhiteSpace(data.CompanySector))
                        content.Item().Field("Sector", data.CompanySector);
                    if (!string.IsNullOrWhiteSpace(data.CompanyAddress))
                        content.Item().Field("Dirección", data.CompanyAddress);
                    if (!string.IsNullOrWhiteSpace(data.CompanyContactName))
                        content.Item().Field("Contacto", data.CompanyContactName);
                    if (!string.IsNullOrWhiteSpace(data.CompanyContactEmail))
                        content.Item().Field("Correo de Contacto", data.CompanyContactEmail);
                    if (!string.IsNullOrWhiteSpace(data.CompanyContactPhone))
                        content.Item().Field("Teléfono de Contacto", data.CompanyContactPhone);
                    content.Item().Field("Asesor Interno", data.AdvisorName);

                    content.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Text("Descripción del Anteproyecto").FontSize(11).Bold().FontColor(TecnmBlue);
                    });
                    content.Item().Field("Título del Proyecto", data.Title);
                    if (!string.IsNullOrWhiteSpace(data.ProjectType))
                        content.Item().Field("Tipo de Proyecto", data.ProjectType);
                    content.Item().Field("Planteamiento del Problema", data.ProblemStatement);
                    content.Item().Field("Justificación", data.Justification);
                    content.Item().Field("Objetivo General", data.GeneralObjective);

                    if (data.SpecificObjectives.Count > 0)
                    {
                        content.Item().Field("Objetivos Específicos",
                            string.Join("\n", data.SpecificObjectives.Select((o, i) => $"{i + 1}. {o}")));
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);

                    column.Item().PaddingTop(4).AlignRight().Text(x =>
                    {
                        x.Span("TecNM Campus Monclova - Sistema de Residencias Profesionales | Generado: ").FontSize(7).FontColor(Colors.Grey.Darken1);
                        x.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(7).SemiBold().FontColor(TecnmGold);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void Field(this IContainer container, string label, string value)
    {
        container.Row(row =>
        {
            row.ConstantItem(170).Text(label).SemiBold().FontColor(Colors.Grey.Darken2);
            row.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value);
        });
    }
}
