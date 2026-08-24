using HtmlAgilityPack;
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

    public static byte[] GeneratePresentationLetterPdf(PresentationLetterData data, string? templateHtml = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        if (!string.IsNullOrWhiteSpace(templateHtml))
        {
            try
            {
                var replacedHtml = ReplaceTemplateVariables(templateHtml, data);
                return GeneratePdfFromHtml(replacedHtml);
            }
            catch
            {
                // Fallback to default layout if dynamic parsing fails
            }
        }

        return GenerateDefaultPdf(data);
    }

    public static string ReplaceTemplateVariables(string templateHtml, PresentationLetterData data)
    {
        if (string.IsNullOrWhiteSpace(templateHtml)) return templateHtml;

        var dateStr = $"{data.IssueDate:dd} de {GetMonthName(data.IssueDate.Month)} de {data.IssueDate:yyyy}";

        return templateHtml
            .Replace("[NOMBRE_ALUMNO]", data.StudentFullName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[nombre_alumno]", data.StudentFullName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[ALUMNO]", data.StudentFullName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[NOMBRE]", data.StudentFullName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[MATRICULA]", data.ControlNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("[matricula]", data.ControlNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("[CONTROL]", data.ControlNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("[NO_CONTROL]", data.ControlNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("[CARRERA]", data.CareerName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[carrera]", data.CareerName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[EMPRESA]", data.CompanyName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[empresa]", data.CompanyName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("[FECHA]", dateStr, StringComparison.OrdinalIgnoreCase)
            .Replace("[fecha]", dateStr, StringComparison.OrdinalIgnoreCase)
            .Replace("[FOLIO]", data.FolioNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("[folio]", data.FolioNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{{nombre_alumno}}", data.StudentFullName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{matricula}}", data.ControlNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{{carrera}}", data.CareerName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{empresa}}", data.CompanyName.ToUpper(), StringComparison.OrdinalIgnoreCase)
            .Replace("{{fecha}}", dateStr, StringComparison.OrdinalIgnoreCase)
            .Replace("{{folio}}", data.FolioNumber, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] GeneratePdfFromHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial").FontColor(Colors.Grey.Darken4));

                page.Content().Column(col =>
                {
                    RenderNodeToQuestPdf(col, body);
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });

        return pdf.GeneratePdf();
    }

    private static void RenderNodeToQuestPdf(ColumnDescriptor col, HtmlNode rootNode)
    {
        foreach (var node in rootNode.ChildNodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    col.Item().PaddingBottom(4).Text(text);
                }
                continue;
            }

            var name = node.Name.ToLowerInvariant();

            if (name == "p" || name == "div" || name.StartsWith("h"))
            {
                var styleAttr = node.GetAttributeValue("style", "");
                var align = ExtractStyleValue(styleAttr, "text-align")?.ToLowerInvariant();
                var marginBottom = ExtractLengthPt(styleAttr, "margin-bottom") ?? (name.StartsWith("h") ? 10f : 6f);
                var marginTop = ExtractLengthPt(styleAttr, "margin-top") ?? (name.StartsWith("h") ? 12f : 0f);

                var pFontSize = ExtractFontSizePt(styleAttr);
                var pColor = ExtractStyleValue(styleAttr, "color");
                var pFontFamily = ExtractStyleValue(styleAttr, "font-family")?.Replace("'", "")?.Replace("\"", "");
                var pIsBold = name.StartsWith("h") || styleAttr.Contains("font-weight: bold") || styleAttr.Contains("font-weight:bold");
                var pIsItalic = styleAttr.Contains("font-style: italic") || styleAttr.Contains("font-style:italic");

                var item = col.Item().PaddingTop((float)marginTop).PaddingBottom((float)marginBottom);

                item.Text(textDescriptor =>
                {
                    if (align == "center") textDescriptor.AlignCenter();
                    else if (align == "right") textDescriptor.AlignRight();
                    else if (align == "justify" || align == "both") textDescriptor.Justify();
                    else textDescriptor.AlignLeft();

                    RenderInlineNodes(textDescriptor, node, new ParentStyle
                    {
                        FontSizePt = pFontSize,
                        ColorHex = pColor,
                        FontFamily = pFontFamily,
                        IsBold = pIsBold,
                        IsItalic = pIsItalic
                    });
                });
            }
            else if (name == "table")
            {
                col.Item().PaddingVertical(8).Table(table =>
                {
                    var rows = node.SelectNodes(".//tr");
                    if (rows == null) return;

                    var firstRow = rows.FirstOrDefault();
                    var colCount = firstRow?.SelectNodes("./td|./th")?.Count ?? 1;

                    table.ColumnsDefinition(columns =>
                    {
                        for (int i = 0; i < colCount; i++) columns.RelativeColumn();
                    });

                    foreach (var row in rows)
                    {
                        var cells = row.SelectNodes("./td|./th");
                        if (cells == null) continue;

                        foreach (var cell in cells)
                        {
                            table.Cell().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(cellCol =>
                            {
                                RenderNodeToQuestPdf(cellCol, cell);
                            });
                        }
                    }
                });
            }
        }
    }

    private class ParentStyle
    {
        public double? FontSizePt { get; set; }
        public string? ColorHex { get; set; }
        public string? FontFamily { get; set; }
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
    }

    private static void RenderInlineNodes(TextDescriptor textDesc, HtmlNode parentNode, ParentStyle parentStyle)
    {
        foreach (var child in parentNode.ChildNodes)
        {
            if (child.Name.Equals("br", StringComparison.OrdinalIgnoreCase))
            {
                textDesc.Span("\n");
                continue;
            }

            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = HtmlEntity.DeEntitize(child.InnerText);
                if (string.IsNullOrEmpty(text)) continue;

                var span = textDesc.Span(text);
                ApplyStyleToSpan(span, parentStyle.FontSizePt, parentStyle.ColorHex, parentStyle.FontFamily, parentStyle.IsBold, parentStyle.IsItalic, false);
                continue;
            }

            var tag = child.Name.ToLowerInvariant();
            var styleAttr = child.GetAttributeValue("style", "");

            var fontSizePt = ExtractFontSizePt(styleAttr) ?? parentStyle.FontSizePt;
            var colorHex = ExtractStyleValue(styleAttr, "color") ?? parentStyle.ColorHex;
            var fontFamily = ExtractStyleValue(styleAttr, "font-family")?.Replace("'", "")?.Replace("\"", "") ?? parentStyle.FontFamily;

            var isBold = parentStyle.IsBold || tag == "strong" || tag == "b" || styleAttr.Contains("font-weight: bold") || styleAttr.Contains("font-weight:bold");
            var isItalic = parentStyle.IsItalic || tag == "em" || tag == "i" || styleAttr.Contains("font-style: italic") || styleAttr.Contains("font-style:italic");
            var isUnderline = tag == "u" || styleAttr.Contains("text-decoration: underline") || styleAttr.Contains("text-decoration:underline");

            if (child.HasChildNodes && child.ChildNodes.Any(c => c.NodeType != HtmlNodeType.Text))
            {
                RenderInlineNodes(textDesc, child, new ParentStyle
                {
                    FontSizePt = fontSizePt,
                    ColorHex = colorHex,
                    FontFamily = fontFamily,
                    IsBold = isBold,
                    IsItalic = isItalic
                });
            }
            else
            {
                var rawText = HtmlEntity.DeEntitize(child.InnerText);
                if (string.IsNullOrEmpty(rawText)) continue;

                var span = textDesc.Span(rawText);
                ApplyStyleToSpan(span, fontSizePt, colorHex, fontFamily, isBold, isItalic, isUnderline);
            }
        }
    }

    private static void ApplyStyleToSpan(TextSpanDescriptor span, double? fontSizePt, string? colorHex, string? fontFamily, bool isBold, bool isItalic, bool isUnderline)
    {
        if (isBold) span.Bold();
        if (isItalic) span.Italic();
        if (isUnderline) span.Underline();
        if (fontSizePt.HasValue) span.FontSize((float)fontSizePt.Value);
        if (!string.IsNullOrEmpty(fontFamily)) span.FontFamily(fontFamily);
        if (!string.IsNullOrEmpty(colorHex) && colorHex.StartsWith("#"))
        {
            try { span.FontColor(Color.FromHex(colorHex)); } catch { }
        }
    }

    private static double? ExtractLengthPt(string styleString, string key)
    {
        var val = ExtractStyleValue(styleString, key);
        if (string.IsNullOrEmpty(val)) return null;
        val = val.Replace("pt", "").Replace("px", "").Trim();
        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pt))
        {
            return pt;
        }
        return null;
    }

    private static string? ExtractStyleValue(string styleString, string key)
    {
        if (string.IsNullOrEmpty(styleString)) return null;
        var parts = styleString.Split(';');
        foreach (var part in parts)
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return kv[1].Trim();
            }
        }
        return null;
    }

    private static double? ExtractFontSizePt(string styleString)
    {
        var val = ExtractStyleValue(styleString, "font-size");
        if (string.IsNullOrEmpty(val)) return null;
        val = val.Replace("pt", "").Replace("px", "").Trim();
        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var pt))
        {
            return pt;
        }
        return null;
    }

    private static byte[] GenerateDefaultPdf(PresentationLetterData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(45);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken4).FontFamily("Arial"));

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

                    content.Item().PaddingTop(30).AlignCenter().Column(col =>
                    {
                        col.Item().Text("ATENTAMENTE").FontSize(10).Bold().FontColor(TecnmBlue).AlignCenter();
                        col.Item().Text("\"Excelencia en Educación Tecnológica\"").FontSize(9).Italic().FontColor(TecnmGold).AlignCenter();
                        col.Item().PaddingTop(45).Text("_________________________________________").AlignCenter();
                        col.Item().Text("ING. OFICINA DE VINCULACIÓN Y GESTIÓN").FontSize(10).Bold().AlignCenter();
                        col.Item().Text("Instituto Tecnológico de Monclova").FontSize(9).FontColor(Colors.Grey.Darken2).AlignCenter();
                    });
                });

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
