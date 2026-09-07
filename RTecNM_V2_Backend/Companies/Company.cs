using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? LegalName { get; set; }
    public string? TradeName { get; set; }
    public string? Rfc { get; set; }
    public string? Sector { get; set; }
    public string? Address { get; set; }

    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Colonia { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }

    public string ContactName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }

    public bool HasAgreement { get; set; } = false;
    public CompanyAgreement? Agreement { get; set; }
}
