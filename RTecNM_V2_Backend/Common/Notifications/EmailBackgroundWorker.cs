using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

using Microsoft.Extensions.DependencyInjection;
using TecNM.Residency.Common.Settings;

namespace TecNM.Residency.Common.Notifications;

public class EmailBackgroundWorker : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailBackgroundWorker> _logger;

    public EmailBackgroundWorker(
        IEmailQueue queue,
        IServiceProvider serviceProvider,
        IOptions<SmtpOptions> options,
        ILogger<EmailBackgroundWorker> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[EmailBackgroundWorker] iniciado. (UseMockInDev={UseMockInDev}, Host={Host}:{Port})",
            _options.UseMockInDev, _options.Host, _options.Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var msg = await _queue.DequeueAsync(stoppingToken);
                await SendEmailAsync(msg, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ERROR] Error procesando correo de la cola.");
            }
        }
    }

    private async Task SendEmailAsync(EmailMessageDto msg, CancellationToken ct)
    {
        SmtpConfigDto config;
        using (var scope = _serviceProvider.CreateScope())
        {
            var settingService = scope.ServiceProvider.GetRequiredService<ISystemSettingService>();
            config = await settingService.GetSmtpConfigAsync();
        }

        var bccCount = msg.BccEmails?.Count ?? 0;
        var destinationLabel = bccCount > 0
            ? (!string.IsNullOrWhiteSpace(msg.ToEmail) ? $"{msg.ToEmail} (+{bccCount} BCC)" : $"Lote masivo ({bccCount} BCC)")
            : msg.ToEmail;

        _logger.LogInformation("[PROCESANDO CORREO] Para '{Destination}' | Asunto: '{Subject}' | Host={Host}:{Port}", destinationLabel, msg.Subject, config.Host, config.Port);

        if (config.UseMockInDev ||
            string.IsNullOrWhiteSpace(config.Username) ||
            string.IsNullOrWhiteSpace(config.Password) ||
            config.Password.Contains("TU_CONTRASEÑA", StringComparison.OrdinalIgnoreCase) ||
            config.SenderEmail.Contains("ejemplo.tecnm.mx", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[MOCK EMAIL DISPATCH] Correo retenido en modo seguro / desarrollo:\n  Para: {Destination} ({ToName})\n  Asunto: {Subject}\n  Total BCC: {BccCount}",
                destinationLabel, msg.ToName, msg.Subject, bccCount);
            return;
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(config.SenderName, config.SenderEmail));

            if (!string.IsNullOrWhiteSpace(msg.ToEmail))
            {
                mime.To.Add(new MailboxAddress(msg.ToName ?? msg.ToEmail, msg.ToEmail));
            }
            else if (bccCount > 0)
            {
                var groupName = string.IsNullOrWhiteSpace(msg.ToName) ? "Comunidad TecNM" : msg.ToName;
                mime.To.Add(new MailboxAddress(groupName, config.SenderEmail));
            }

            if (msg.BccEmails != null && bccCount > 0)
            {
                foreach (var bcc in msg.BccEmails.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    mime.Bcc.Add(new MailboxAddress("", bcc.Trim()));
                }
            }

            mime.Subject = msg.Subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = msg.BodyHtml
            };

            if (msg.Attachments != null && msg.Attachments.Count > 0)
            {
                foreach (var att in msg.Attachments)
                {
                    bodyBuilder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType ?? "application/pdf"));
                }
            }

            mime.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = config.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(config.Host, config.Port, secureOption, ct);
            await client.AuthenticateAsync(config.Username, config.Password, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("[EXITO] Correo enviado exitosamente vía SMTP a '{Destination}' (BCC={BccCount})", destinationLabel, bccCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ADVERTENCIA] Error enviando correo vía SMTP a '{Destination}'. Mensaje retenido.", destinationLabel);
        }
    }
}
