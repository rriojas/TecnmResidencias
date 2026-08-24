namespace TecNM.Residency.Common.Settings;

public interface ISystemSettingService
{
    Task<SmtpConfigDto> GetSmtpConfigAsync();
    Task<Result<bool>> UpdateSmtpConfigAsync(SmtpConfigDto dto, long userId);
    Task<Result<bool>> TestSmtpConnectionAsync(string recipientEmail, SmtpConfigDto? testConfig = null);
    Task<string> GetPresentationLetterTemplateAsync();
    Task<Result<bool>> UpdatePresentationLetterTemplateAsync(string htmlContent, long userId);
    Task<Result<bool>> ResetPresentationLetterTemplateAsync(long userId);
}
