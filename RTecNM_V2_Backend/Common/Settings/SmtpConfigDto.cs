namespace TecNM.Residency.Common.Settings;

public class SmtpConfigDto
{
    public string Host { get; set; } = "smtp.office365.com";
    public int Port { get; set; } = 587;
    public string SenderName { get; set; } = "TecNM Residencias";
    public string SenderEmail { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public bool UseMockInDev { get; set; } = true;
}
