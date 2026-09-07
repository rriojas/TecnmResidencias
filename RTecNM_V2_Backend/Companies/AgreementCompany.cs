namespace TecNM.Residency.Companies;

public class AgreementCompany
{
    public long AgreementId { get; set; }
    public CompanyAgreement? Agreement { get; set; }

    public long CompanyId { get; set; }
    public Company? Company { get; set; }

    public string? AgreementScope { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
