using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Rfc { get; set; } = string.Empty;
    public string? Sector { get; set; }
    public string? Address { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
}
