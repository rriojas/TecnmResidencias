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
        _logger.LogInformation("📧 EmailBackgroundWorker iniciado. (UseMockInDev={UseMockInDev}, Host={Host}:{Port})",
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
                _logger.LogError(ex, "❌ Error procesando correo de la cola.");
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

        _logger.LogInformation("📬 Procesando correo para '{ToEmail}' | Asunto: '{Subject}' | Host={Host}:{Port}", msg.ToEmail, msg.Subject, config.Host, config.Port);

        if (config.UseMockInDev || string.IsNullOrWhiteSpace(config.Username) || string.IsNullOrWhiteSpace(config.Password))
        {
            _logger.LogInformation("🌐 [MOCK EMAIL DISPATCH] Correo enviado simbólicamente en desarrollo:\n  Para: {ToEmail} ({ToName})\n  Asunto: {Subject}",
                msg.ToEmail, msg.ToName, msg.Subject);
            return;
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(config.SenderName, config.SenderEmail));
            mime.To.Add(new MailboxAddress(msg.ToName ?? msg.ToEmail, msg.ToEmail));
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

            _logger.LogInformation("✅ Correo enviado exitosamente vía SMTP a '{ToEmail}'", msg.ToEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Error enviando correo vía SMTP a '{ToEmail}'. Mensaje retenido.", msg.ToEmail);
        }
    }
}
