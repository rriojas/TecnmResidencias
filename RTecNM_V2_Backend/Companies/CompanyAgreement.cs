using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class CompanyAgreement : BaseEntity
{
    public long CompanyId { get; set; }
    public Company? Company { get; set; }

    public string? ArchiveId { get; set; }
    public string Status { get; set; } = "VIGENTE";
    public string? PitCode { get; set; }
    public string? CiaType { get; set; }
    public string? AgreementScope { get; set; }
    public string? Sector { get; set; }
    public string? BusinessLine { get; set; }
    public string? CompanySize { get; set; }
    public string? GeographicScope { get; set; }
    public string? Notes { get; set; }
}
