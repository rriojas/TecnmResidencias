using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class CompanyAgreement : BaseEntity
{
    public string? ArchiveId { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public string? ProcessStatus { get; set; } // EN_RENOVACION, CANCELADO, NULL
    public string? PitCode { get; set; }
    public string? CiaType { get; set; }

    public string CalculateStatus()
    {
        if (string.Equals(ProcessStatus, "CANCELADO", StringComparison.OrdinalIgnoreCase))
            return "CANCELADO";

        if (!ExpirationDate.HasValue)
            return "VIGENTE";

        if (ExpirationDate.Value.Date >= DateTime.UtcNow.Date)
            return "VIGENTE";

        if (string.Equals(ProcessStatus, "EN_RENOVACION", StringComparison.OrdinalIgnoreCase))
            return "EN RENOVACIÓN";

        return "VENCIDO";
    }

    public bool IsActiveAgreement =>
        !string.Equals(ProcessStatus, "CANCELADO", StringComparison.OrdinalIgnoreCase) &&
        (!ExpirationDate.HasValue || ExpirationDate.Value.Date >= DateTime.UtcNow.Date);
    public string? AgreementScope { get; set; }
    public string? Sector { get; set; }
    public string? BusinessLine { get; set; }
    public string? CompanySize { get; set; }
    public string? GeographicScope { get; set; }
    public string? Notes { get; set; }

    public ICollection<AgreementCompany> AgreementCompanies { get; set; } = new List<AgreementCompany>();
}
