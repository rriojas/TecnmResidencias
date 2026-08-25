using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MailKit.Net.Smtp;
using MailKit.Security;
using Mammoth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using TecNM.Residency.Common.Notifications;

namespace TecNM.Residency.Common.Settings;

public class SystemSettingService : ISystemSettingService
{
    private readonly AppDbContext _context;
    private readonly SmtpOptions _defaultSmtpOptions;

    public SystemSettingService(AppDbContext context, IOptions<SmtpOptions> defaultSmtpOptions)
    {
        _context = context;
        _defaultSmtpOptions = defaultSmtpOptions.Value;
    }

    public async Task<SmtpConfigDto> GetSmtpConfigAsync()
    {
        var settings = await _context.SystemSettings
            .Where(s => s.Key.StartsWith("smtp."))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        return new SmtpConfigDto
        {
            Host = settings.TryGetValue("smtp.host", out var h) && !string.IsNullOrWhiteSpace(h) ? h : (_defaultSmtpOptions.Host ?? "smtp.gmail.com"),
            Port = settings.TryGetValue("smtp.port", out var p) && int.TryParse(p, out var portVal) ? portVal : (_defaultSmtpOptions.Port != 0 ? _defaultSmtpOptions.Port : 587),
            SenderName = settings.TryGetValue("smtp.sender_name", out var sn) && !string.IsNullOrWhiteSpace(sn) ? sn : (_defaultSmtpOptions.SenderName ?? "TecNM Residencias"),
            SenderEmail = settings.TryGetValue("smtp.sender_email", out var se) && !string.IsNullOrWhiteSpace(se) ? se : (_defaultSmtpOptions.SenderEmail ?? string.Empty),
            Username = settings.TryGetValue("smtp.username", out var u) && !string.IsNullOrWhiteSpace(u) ? u : (_defaultSmtpOptions.Username ?? string.Empty),
            Password = settings.TryGetValue("smtp.password", out var pwd) && !string.IsNullOrWhiteSpace(pwd) ? pwd : (_defaultSmtpOptions.Password ?? string.Empty),
            EnableSsl = settings.TryGetValue("smtp.enable_ssl", out var ssl) ? (bool.TryParse(ssl, out var sslVal) && sslVal) : _defaultSmtpOptions.EnableSsl,
            UseMockInDev = settings.TryGetValue("smtp.use_mock", out var mock) ? (bool.TryParse(mock, out var mockVal) && mockVal) : _defaultSmtpOptions.UseMockInDev
        };
    }

    public async Task<Result<bool>> UpdateSmtpConfigAsync(SmtpConfigDto dto, long userId)
    {
        var map = new Dictionary<string, (string Value, string Desc)>
        {
            ["smtp.host"] = (dto.Host ?? "", "Servidor SMTP Host"),
            ["smtp.port"] = (dto.Port.ToString(), "Puerto SMTP"),
            ["smtp.sender_name"] = (dto.SenderName ?? "", "Nombre del Remitente"),
            ["smtp.sender_email"] = (dto.SenderEmail ?? "", "Correo del Remitente"),
            ["smtp.username"] = (dto.Username ?? "", "Usuario SMTP"),
            ["smtp.password"] = (dto.Password ?? "", "Contraseña SMTP"),
            ["smtp.enable_ssl"] = (dto.EnableSsl.ToString().ToLowerInvariant(), "SSL/TLS Activado"),
            ["smtp.use_mock"] = (dto.UseMockInDev.ToString().ToLowerInvariant(), "Modo Simulación Activo")
        };

        foreach (var kv in map)
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == kv.Key);
            if (setting is null)
            {
                _context.SystemSettings.Add(new SystemSetting
                {
                    Key = kv.Key,
                    Value = kv.Value.Value,
                    Description = kv.Value.Desc,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = userId
                });
            }
            else
            {
                setting.Value = kv.Value.Value;
                setting.UpdatedAt = DateTime.UtcNow;
                setting.UpdatedBy = userId;
                _context.SystemSettings.Update(setting);
            }
        }

        await _context.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> TestSmtpConnectionAsync(string recipientEmail, SmtpConfigDto? testConfig = null)
    {
        var config = testConfig ?? await GetSmtpConfigAsync();

        if (string.IsNullOrWhiteSpace(recipientEmail))
            return Result<bool>.Failure("Debe ingresar un correo electrónico de destino para la prueba.");

        if (config.UseMockInDev)
        {
            return Result<bool>.Failure("El sistema se encuentra en modo simulación ('UseMockInDev: true'). Desactiva el modo simulación para realizar un envío de correo real a tu bandeja.");
        }

        if (string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.Password))
        {
            return Result<bool>.Failure("Debe proporcionar un Usuario y Contraseña SMTP válidos.");
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(config.SenderName, config.SenderEmail));
            mime.To.Add(new MailboxAddress(recipientEmail, recipientEmail));
            mime.Subject = "✅ Correo de Prueba — Sistema de Residencias TecNM Monclova";

            var body = new BodyBuilder
            {
                HtmlBody = $@"
<div style=""font-family: Arial, sans-serif; padding: 20px; color: #1B396A;"">
    <h2>Prueba de Conexión SMTP Exitosa</h2>
    <p>Este es un correo de prueba enviado desde el <strong>Sistema de Residencias del TecNM Monclova</strong>.</p>
    <ul>
        <li><strong>Servidor Host:</strong> {config.Host}</li>
        <li><strong>Puerto:</strong> {config.Port}</li>
        <li><strong>Remitente:</strong> {config.SenderEmail}</li>
        <li><strong>Fecha:</strong> {DateTime.Now:g}</li>
    </ul>
    <p style=""color: #27ae60; font-weight: bold;"">¡La configuración SMTP se encuentra activa y funcionando correctamente!</p>
</div>"
            };
            mime.Body = body.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = config.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(config.Host, config.Port, secureOption);
            await client.AuthenticateAsync(config.Username, config.Password);
            await client.SendAsync(mime);
            await client.DisconnectAsync(true);

            return Result<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure($"Error al conectar con el servidor SMTP: {ex.Message}");
        }
    }

    public async Task<string> GetPresentationLetterTemplateAsync()
    {
        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "template.presentation_letter_html");
        if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
        {
            return setting.Value;
        }

        return GetDefaultPresentationLetterTemplateHtml();
    }

    public async Task<Result<bool>> UpdatePresentationLetterTemplateAsync(string htmlContent, long userId)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            return Result<bool>.Failure("El contenido HTML de la plantilla no puede estar vacío.");

        var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "template.presentation_letter_html");
        if (setting is null)
        {
            _context.SystemSettings.Add(new SystemSetting
            {
                Key = "template.presentation_letter_html",
                Value = htmlContent,
                Description = "Plantilla HTML oficial para Carta de Presentación",
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = userId
            });
        }
        else
        {
            setting.Value = htmlContent;
            setting.UpdatedAt = DateTime.UtcNow;
            setting.UpdatedBy = userId;
            _context.SystemSettings.Update(setting);
        }

        await _context.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<string>> UploadWordTemplateAsync(Stream wordStream, long userId)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            await wordStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            string htmlContent;
            try
            {
                htmlContent = ConvertWordDocumentToHtmlWithExactStyles(memoryStream);
            }
            catch
            {
                // Fallback to Mammoth if OpenXml stream parsing encounters complex non-standard elements
                memoryStream.Position = 0;
                var converter = new DocumentConverter();
                var result = converter.ConvertToHtml(memoryStream);
                htmlContent = $"<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"UTF-8\"><style>body{{font-family:'Segoe UI',Arial,sans-serif;margin:40px;line-height:1.6;}}</style></head><body>{result.Value}</body></html>";
            }

            await UpdatePresentationLetterTemplateAsync(htmlContent, userId);
            return Result<string>.Success(htmlContent);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Error al procesar el archivo Word (.docx): {ex.Message}");
        }
    }

    private static string ConvertWordDocumentToHtmlWithExactStyles(MemoryStream stream)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body == null) return "<!DOCTYPE html><html><body></body></html>";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"es\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <title>Carta de Presentación Oficial</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: 'Segoe UI', Arial, sans-serif; color: #2c3e50; margin: 40px; line-height: 1.6; }");
        sb.AppendLine("        p { margin-bottom: 10pt; margin-top: 0; }");
        sb.AppendLine("        table { width: 100%; border-collapse: collapse; margin: 15px 0; }");
        sb.AppendLine("        td, th { border: 1px solid #cbd5e1; padding: 8px; vertical-align: top; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph p)
            {
                sb.AppendLine(ConvertParagraphToHtmlWithStyles(p));
            }
            else if (element is Table tbl)
            {
                sb.AppendLine(ConvertTableToHtmlWithStyles(tbl));
            }
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string ConvertParagraphToHtmlWithStyles(Paragraph p)
    {
        var pPr = p.ParagraphProperties;
        var styles = new List<string>();

        if (pPr?.Justification?.Val?.Value != null)
        {
            var align = pPr.Justification.Val.Value.ToString()?.ToLowerInvariant();
            if (align == "center") styles.Add("text-align: center;");
            else if (align == "right") styles.Add("text-align: right;");
            else if (align == "both" || align == "justify") styles.Add("text-align: justify;");
            else if (align == "left") styles.Add("text-align: left;");
        }

        if (pPr?.SpacingBetweenLines != null)
        {
            if (pPr.SpacingBetweenLines.After?.Value != null && int.TryParse(pPr.SpacingBetweenLines.After.Value, out var afterVal))
            {
                styles.Add($"margin-bottom: {afterVal / 20.0}pt;");
            }
            if (pPr.SpacingBetweenLines.Before?.Value != null && int.TryParse(pPr.SpacingBetweenLines.Before.Value, out var beforeVal))
            {
                styles.Add($"margin-top: {beforeVal / 20.0}pt;");
            }
        }

        var styleAttr = styles.Count > 0 ? $" style=\"{string.Join(" ", styles)}\"" : "";
        var innerHtml = new StringBuilder();

        foreach (var child in p.ChildElements)
        {
            if (child is Run run)
            {
                innerHtml.Append(ConvertRunToHtmlWithStyles(run));
            }
            else if (child is SimpleField field)
            {
                foreach (var fieldRun in field.Elements<Run>())
                {
                    innerHtml.Append(ConvertRunToHtmlWithStyles(fieldRun));
                }
            }
        }

        var content = innerHtml.ToString();
        if (string.IsNullOrWhiteSpace(content)) return "<p>&nbsp;</p>";

        return $"<p{styleAttr}>{content}</p>";
    }

    private static string ConvertRunToHtmlWithStyles(Run run)
    {
        var rPr = run.RunProperties;
        var rawText = string.Join("", run.Elements<Text>().Select(t => t.Text));

        if (string.IsNullOrEmpty(rawText)) return "";
        var text = System.Net.WebUtility.HtmlEncode(rawText);

        var runStyles = new List<string>();

        if (rPr?.RunFonts?.Ascii?.Value != null)
        {
            runStyles.Add($"font-family: '{rPr.RunFonts.Ascii.Value}', sans-serif;");
        }

        if (rPr?.FontSize?.Val?.Value != null && double.TryParse(rPr.FontSize.Val.Value, out var halfPts))
        {
            runStyles.Add($"font-size: {halfPts / 2.0}pt;");
        }

        if (rPr?.Color?.Val?.Value != null && rPr.Color.Val.Value != "auto")
        {
            var hexColor = rPr.Color.Val.Value;
            if (!hexColor.StartsWith("#")) hexColor = "#" + hexColor;
            runStyles.Add($"color: {hexColor};");
        }

        var isBold = rPr?.Bold != null && (rPr.Bold.Val == null || rPr.Bold.Val.Value);
        var isItalic = rPr?.Italic != null && (rPr.Italic.Val == null || rPr.Italic.Val.Value);
        var isUnderline = rPr?.Underline != null;

        if (isBold) runStyles.Add("font-weight: bold;");
        if (isItalic) runStyles.Add("font-style: italic;");
        if (isUnderline) runStyles.Add("text-decoration: underline;");

        if (runStyles.Count > 0)
        {
            return $"<span style=\"{string.Join(" ", runStyles)}\">{text}</span>";
        }

        return text;
    }

    private static string ConvertTableToHtmlWithStyles(Table tbl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<table>");

        foreach (var row in tbl.Elements<TableRow>())
        {
            sb.AppendLine("  <tr>");
            foreach (var cell in row.Elements<TableCell>())
            {
                sb.Append("    <td>");
                foreach (var p in cell.Elements<Paragraph>())
                {
                    sb.Append(ConvertParagraphToHtmlWithStyles(p));
                }
                sb.AppendLine("</td>");
            }
            sb.AppendLine("  </tr>");
        }

        sb.AppendLine("</table>");
        return sb.ToString();
    }

    public async Task<Result<bool>> ResetPresentationLetterTemplateAsync(long userId)
    {
        var defaultHtml = GetDefaultPresentationLetterTemplateHtml();
        return await UpdatePresentationLetterTemplateAsync(defaultHtml, userId);
    }

    private static string GetDefaultPresentationLetterTemplateHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <title>Carta de Presentación Oficial</title>
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; color: #2c3e50; margin: 40px; }
        .header { text-align: center; border-bottom: 2px solid #C5A059; padding-bottom: 15px; margin-bottom: 30px; }
        .institution { color: #1B396A; font-size: 18px; font-weight: bold; margin: 0; }
        .sub-institution { color: #1B396A; font-size: 14px; font-weight: 600; margin: 4px 0; }
        .dept { color: #C5A059; font-size: 12px; font-weight: bold; margin: 0; }
        .meta { text-align: right; margin-bottom: 25px; font-size: 12px; }
        .subject { font-weight: bold; color: #1B396A; }
        .recipient { font-weight: bold; font-size: 14px; margin-bottom: 20px; }
        .body-text { line-height: 1.6; font-size: 13px; margin-bottom: 15px; text-align: justify; }
        .highlight { font-weight: bold; color: #1B396A; }
        .signature-box { text-align: center; margin-top: 60px; }
        .signature-line { border-top: 1px solid #2c3e50; width: 280px; margin: 0 auto 8px auto; }
    </style>
</head>
<body>
    <div class=""header"">
        <h1 class=""institution"">TECNOLÓGICO NACIONAL DE MÉXICO</h1>
        <h2 class=""sub-institution"">INSTITUTO TECNOLÓGICO DE MONCLOVA</h2>
        <p class=""dept"">DEPARTAMENTO DE GESTIÓN TECNOLÓGICA Y VINCULACIÓN</p>
    </div>

    <div class=""meta"">
        <p><span class=""subject"">Asunto:</span> Carta de Presentación de Residencia Profesional</p>
        <p><strong>Folio:</strong> [FOLIO]</p>
        <p>Monclova, Coahuila; a [FECHA].</p>
    </div>

    <div class=""recipient"">
        <p>[EMPRESA]<br>PRESENTE.</p>
    </div>

    <p class=""body-text"">
        Por medio de la presente, el <span class=""highlight"">Instituto Tecnológico de Monclova</span> presenta formalmente al C. <strong>[NOMBRE_ALUMNO]</strong>, con número de control <strong>[MATRICULA]</strong>, alumno(a) inscrito(a) en el programa educativo de <strong>[CARRERA]</strong>.
    </p>

    <p class=""body-text"">
        Quien ha cubierto los créditos académicos requeridos conforme a los lineamientos vigentes del Tecnológico Nacional de México, encontrándose en aptitud para desarrollar su <strong>Residencia Profesional</strong> con una duración obligatoria de 500 horas lectivas en su prestigiada institución o empresa.
    </p>

    <p class=""body-text"">
        Agradecemos las facilidades brindadas al estudiante para el desarrollo de su proyecto, el cual contribuirá significativamente al fortalecimiento de sus competencias profesionales y al desarrollo tecnológico de la región.
    </p>

    <p class=""body-text"">Sin otro particular por el momento, aprovecho la ocasión para enviarle un cordial y respetuoso saludo.</p>

    <div class=""signature-box"">
        <p style=""color:#1B396A; font-weight:bold; margin-bottom:4px;"">ATENTAMENTE</p>
        <p style=""color:#C5A059; font-style:italic; font-size:12px; margin-top:0;"">""Excelencia en Educación Tecnológica""</p>
        <br><br><br>
        <div class=""signature-line""></div>
        <p style=""font-weight:bold; margin:0;"">OFICINA DE VINCULACIÓN Y GESTIÓN TECNOLÓGICA</p>
        <p style=""font-size:12px; color:#6c757d; margin:2px 0 0 0;"">Instituto Tecnológico de Monclova</p>
    </div>
</body>
</html>";
    }
}
