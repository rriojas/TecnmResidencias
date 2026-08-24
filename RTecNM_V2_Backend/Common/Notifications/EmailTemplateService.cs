namespace TecNM.Residency.Common.Notifications;

public interface IEmailTemplateService
{
    EmailMessageDto BuildWelcomeEmail(string studentName, string controlNumber, string email, string loginUrl);
    EmailMessageDto BuildLetterAvailableEmail(string studentName, string documentTitle, string loginUrl);
    EmailMessageDto BuildPresentationLetterEmail(string studentName, string controlNumber, string email, string careerName, string companyName, byte[] pdfBytes);
}

public class EmailTemplateService : IEmailTemplateService
{
    public EmailMessageDto BuildPresentationLetterEmail(string studentName, string controlNumber, string email, string careerName, string companyName, byte[] pdfBytes)
    {
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <title>Carta de Presentación Oficial — Residencias Profesionales</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f6f9; font-family:'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f6f9; padding:20px 0;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff; border-radius:8px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.1); border:1px solid #e1e8ed;"">
                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#1B396A; padding:24px 30px; text-align:center;"">
                            <h1 style=""color:#ffffff; margin:0; font-size:22px; font-weight:600; letter-spacing:0.5px;"">
                                Tecnológico Nacional de México
                            </h1>
                            <p style=""color:#C5A059; margin:6px 0 0 0; font-size:14px; font-weight:500;"">
                                Departamento de Gestión Tecnológica y Vinculación
                            </p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:30px; color:#2c3e50;"">
                            <h2 style=""color:#1B396A; margin-top:0; font-size:18px;"">Estimado(a) {studentName},</h2>
                            <p style=""line-height:1.6; font-size:15px;"">
                                Te enviamos adjunta tu <strong>Carta de Presentación Oficial</strong> en formato PDF para el inicio de tu trámite de <strong>Residencia Profesional</strong>.
                            </p>
                            <div style=""background-color:#f8f9fa; border-left:4px solid #C5A059; padding:16px 20px; margin:20px 0; border-radius:4px;"">
                                <p style=""margin:0 0 6px 0; font-size:14px; color:#1B396A;""><strong>Detalles del Alumno:</strong></p>
                                <p style=""margin:0 0 4px 0; font-size:14px;"">• <strong>N° Control:</strong> {controlNumber}</p>
                                <p style=""margin:0 0 4px 0; font-size:14px;"">• <strong>Programa:</strong> {careerName}</p>
                                <p style=""margin:0; font-size:14px;"">• <strong>Institución / Empresa:</strong> {companyName}</p>
                            </div>
                            <p style=""line-height:1.6; font-size:14px; color:#495057;"">
                                El archivo adjunto <strong>Carta_Presentacion_{controlNumber}.pdf</strong> cuenta con el aval del departamento de Vinculación. Imprime o presenta este documento ante la empresa receptora.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#f8f9fa; padding:16px 30px; text-align:center; border-top:1px solid #e9ecef; font-size:12px; color:#868e96;"">
                            Oficina de Vinculación y Gestión Tecnológica — TecNM Campus Monclova.<br />
                            Mensaje generado automáticamente por la plataforma de Residencias.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return new EmailMessageDto
        {
            ToEmail = email,
            ToName = studentName,
            Subject = $"Carta de Presentación Oficial — TecNM Monclova ({controlNumber})",
            BodyHtml = html,
            Attachments = new List<EmailAttachmentDto>
            {
                new EmailAttachmentDto
                {
                    FileName = $"Carta_Presentacion_{controlNumber}.pdf",
                    Content = pdfBytes,
                    ContentType = "application/pdf"
                }
            }
        };
    }
    public EmailMessageDto BuildWelcomeEmail(string studentName, string controlNumber, string email, string loginUrl)
    {
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <title>Bienvenida al Sistema de Residencias Profesionales</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f6f9; font-family:'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f6f9; padding:20px 0;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff; border-radius:8px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.1); border:1px solid #e1e8ed;"">
                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#1B396A; padding:24px 30px; text-align:center;"">
                            <h1 style=""color:#ffffff; margin:0; font-size:22px; font-weight:600; letter-spacing:0.5px;"">
                                Tecnológico Nacional de México
                            </h1>
                            <p style=""color:#C5A059; margin:6px 0 0 0; font-size:14px; font-weight:500;"">
                                Campus Monclova — Residencias Profesionales
                            </p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:30px; color:#2c3e50;"">
                            <h2 style=""color:#1B396A; margin-top:0; font-size:18px;"">¡Bienvenido(a), {studentName}!</h2>
                            <p style=""line-height:1.6; font-size:15px;"">
                                Tu perfil de estudiante ha sido dado de alta en la plataforma oficial de <strong>Residencias Profesionales</strong> del TecNM Campus Monclova.
                            </p>
                            <div style=""background-color:#f8f9fa; border-left:4px solid #C5A059; padding:16px 20px; margin:20px 0; border-radius:4px;"">
                                <p style=""margin:0 0 8px 0; font-size:14px; color:#495057;""><strong>Tus Credenciales de Acceso:</strong></p>
                                <p style=""margin:0 0 4px 0; font-size:14px;"">📧 <strong>Correo:</strong> <span style=""color:#1B396A;"">{email}</span></p>
                                <p style=""margin:0; font-size:14px;"">🔑 <strong>Contraseña Inicial:</strong> Tu N° de Control (<strong>{controlNumber}</strong>)</p>
                            </div>
                            <p style=""line-height:1.6; font-size:14px; color:#6c757d;"">
                                Te sugerimos ingresar a la plataforma para actualizar tu perfil y dar seguimiento a tu anteproyecto y expediente digital.
                            </p>
                            <!-- CTA Button -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:25px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{loginUrl}"" style=""background-color:#1B396A; color:#ffffff; padding:12px 28px; text-decoration:none; font-weight:bold; border-radius:6px; display:inline-block; font-size:15px; border:1px solid #142a4f;"">
                                            Iniciar Sesión en la Plataforma
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#f8f9fa; padding:16px 30px; text-align:center; border-top:1px solid #e9ecef; font-size:12px; color:#868e96;"">
                            Este es un mensaje automático de la División de Estudios Profesionales — TecNM Campus Monclova.<br />
                            Por favor no respondas directamente a este correo.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return new EmailMessageDto
        {
            ToEmail = email,
            ToName = studentName,
            Subject = "Bienvenido al Sistema de Residencias Profesionales — TecNM Monclova",
            BodyHtml = html
        };
    }

    public EmailMessageDto BuildLetterAvailableEmail(string studentName, string documentTitle, string loginUrl)
    {
        var html = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <title>Documento Disponible en Expediente Digital</title>
</head>
<body style=""margin:0; padding:0; background-color:#f4f6f9; font-family:'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;"">
    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f6f9; padding:20px 0;"">
        <tr>
            <td align=""center"">
                <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#ffffff; border-radius:8px; overflow:hidden; box-shadow:0 4px 12px rgba(0,0,0,0.1); border:1px solid #e1e8ed;"">
                    <!-- Header -->
                    <tr>
                        <td style=""background-color:#1B396A; padding:24px 30px; text-align:center;"">
                            <h1 style=""color:#ffffff; margin:0; font-size:22px; font-weight:600; letter-spacing:0.5px;"">
                                Tecnológico Nacional de México
                            </h1>
                            <p style=""color:#C5A059; margin:6px 0 0 0; font-size:14px; font-weight:500;"">
                                Campus Monclova — Expediente Digital
                            </p>
                        </td>
                    </tr>
                    <!-- Body -->
                    <tr>
                        <td style=""padding:30px; color:#2c3e50;"">
                            <h2 style=""color:#1B396A; margin-top:0; font-size:18px;"">Estimado(a) {studentName},</h2>
                            <p style=""line-height:1.6; font-size:15px;"">
                                Te informamos que tu <strong>{documentTitle}</strong> ha sido cargada y verificada en tu expediente digital de residencia profesional.
                            </p>
                            <div style=""background-color:#f8f9fa; border-left:4px solid #1B396A; padding:16px 20px; margin:20px 0; border-radius:4px;"">
                                <p style=""margin:0; font-size:14px; color:#1B396A;"">
                                    📄 <strong>Documento:</strong> {documentTitle}<br />
                                    ✅ <strong>Estatus:</strong> Emitido / Disponible para Descarga
                                </p>
                            </div>
                            <p style=""line-height:1.6; font-size:14px; color:#6c757d;"">
                                Puedes consultar y descargar este documento ingresando a la sección de <strong>Expediente Documental</strong> en la plataforma.
                            </p>
                            <!-- CTA Button -->
                            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin-top:25px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{loginUrl}"" style=""background-color:#1B396A; color:#ffffff; padding:12px 28px; text-decoration:none; font-weight:bold; border-radius:6px; display:inline-block; font-size:15px; border:1px solid #142a4f;"">
                                            Ir a Mi Expediente Digital
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color:#f8f9fa; padding:16px 30px; text-align:center; border-top:1px solid #e9ecef; font-size:12px; color:#868e96;"">
                            Este es un mensaje automático de la División de Estudios Profesionales — TecNM Campus Monclova.<br />
                            Por favor no respondas directamente a este correo.
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        return new EmailMessageDto
        {
            Subject = $"{documentTitle} Disponible en tu Expediente Digital — TecNM Monclova",
            BodyHtml = html
        };
    }
}
