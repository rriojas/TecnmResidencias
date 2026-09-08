using System.Net.Mail;
using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TecNM.Residency.Common.Settings;

namespace TecNM.Residency.Common.EmailVerification;

public class EmailVerificationService : IEmailVerificationService
{
    private readonly InstitutionalEmailOptions _options;
    private readonly ILogger<EmailVerificationService> _logger;
    private readonly ILookupClient _lookupClient;

    public EmailVerificationService(
        IOptions<InstitutionalEmailOptions> options,
        ILogger<EmailVerificationService> logger,
        ILookupClient? lookupClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _lookupClient = lookupClient ?? new LookupClient(new LookupClientOptions
        {
            Timeout = TimeSpan.FromSeconds(4),
            Retries = 1,
            UseCache = true
        });
    }

    public async Task<Result<string>> ValidateAndVerifyEmailAsync(string? email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result<string>.Failure("El correo electrónico es obligatorio.", 400);
        }

        // 1. Sanitize all whitespace and convert to lowercase
        var cleanEmail = StringSanitizer.SanitizeEmail(email);

        // 2. Syntax validation via MailAddress
        if (!MailAddress.TryCreate(cleanEmail, out var mailAddress) || mailAddress.Address != cleanEmail)
        {
            return Result<string>.Failure($"El formato del correo '{cleanEmail}' no es válido.", 400);
        }

        var host = mailAddress.Host.Trim().ToLowerInvariant();

        // 3. Domain validation against configured AllowedDomains
        var allowedDomains = _options.AllowedDomains != null && _options.AllowedDomains.Count > 0
            ? _options.AllowedDomains.Select(d => d.Trim().ToLowerInvariant().TrimStart('@')).ToList()
            : new List<string> { "monclova.tecnm.mx", "tecnm.mx" };

        var matchesAllowed = allowedDomains.Any(domain => 
            host.Equals(domain, StringComparison.OrdinalIgnoreCase) || 
            host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));

        if (!matchesAllowed)
        {
            var allowedFormatted = string.Join(", ", allowedDomains.Select(d => "@" + d));
            return Result<string>.Failure($"El correo debe pertenecer a uno de los dominios institucionales autorizados ({allowedFormatted}).", 400);
        }

        // 4. DNS MX record validation (real existence of destination mail host)
        if (_options.EnableDnsCheck)
        {
            try
            {
                var queryResult = await _lookupClient.QueryAsync(host, QueryType.MX, cancellationToken: cancellationToken);
                var mxRecords = queryResult?.Answers?.MxRecords()?.ToList();

                if (mxRecords == null || mxRecords.Count == 0)
                {
                    _logger.LogWarning("No se encontraron registros MX activos para el dominio '{Host}'.", host);
                    return Result<string>.Failure($"El dominio del correo '{host}' no tiene servidores de correo activos para recibir mensajes.", 400);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error al consultar registros DNS MX para el dominio '{Host}'.", host);
                // Si la consulta DNS falla por timeout o problemas de red temporal, registramos warning pero no bloqueamos
                // a menos que sea un error definitivo de no-existencia de host
                if (ex.Message.Contains("NXDOMAIN", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("Non-Existent Domain", StringComparison.OrdinalIgnoreCase))
                {
                    return Result<string>.Failure($"El dominio de correo '{host}' no existe en el sistema DNS.", 400);
                }
            }
        }

        return Result<string>.Success(cleanEmail);
    }
}
