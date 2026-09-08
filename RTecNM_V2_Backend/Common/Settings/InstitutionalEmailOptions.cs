namespace TecNM.Residency.Common.Settings;

public class InstitutionalEmailOptions
{
    public const string SectionName = "InstitutionalEmail";

    public string PrimaryDomain { get; set; } = "@monclova.tecnm.mx";
    public List<string> AllowedDomains { get; set; } = new() { "@monclova.tecnm.mx", "@tecnm.mx" };
    public bool EnableDnsCheck { get; set; } = true;
}
