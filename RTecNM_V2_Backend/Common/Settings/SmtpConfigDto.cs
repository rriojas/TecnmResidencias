namespace TecNM.Residency.Common.Settings;

public class SmtpConfigDto
{
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string SenderName { get; set; } = "TecNM Residencias Monclova";
    public string SenderEmail { get; set; } = "residencias@monclova.tecnm.mx";
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;
    public bool UseMockInDev { get; set; } = true;
}
