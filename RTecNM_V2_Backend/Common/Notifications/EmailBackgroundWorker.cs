using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TecNM.Residency.Common.Notifications;

public class EmailBackgroundWorker : BackgroundService
{
    private readonly IEmailQueue _queue;
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailBackgroundWorker> _logger;

    public EmailBackgroundWorker(
        IEmailQueue queue,
        IOptions<SmtpOptions> options,
        ILogger<EmailBackgroundWorker> logger)
    {
        _queue = queue;
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
        _logger.LogInformation("📬 Procesando correo para '{ToEmail}' | Asunto: '{Subject}'", msg.ToEmail, msg.Subject);

        if (_options.UseMockInDev || string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password))
        {
            _logger.LogInformation("🌐 [MOCK EMAIL DISPATCH] Correo enviado simbólicamente en desarrollo:\n  Para: {ToEmail} ({ToName})\n  Asunto: {Subject}",
                msg.ToEmail, msg.ToName, msg.Subject);
            return;
        }

        try
        {
            var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(_options.SenderName, _options.SenderEmail));
            mime.To.Add(new MailboxAddress(msg.ToName ?? msg.ToEmail, msg.ToEmail));
            mime.Subject = msg.Subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = msg.BodyHtml
            };
            mime.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var secureOption = _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_options.Host, _options.Port, secureOption, ct);
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);
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
