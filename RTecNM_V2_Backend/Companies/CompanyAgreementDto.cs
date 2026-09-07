namespace TecNM.Residency.Companies;

public record CompanyAgreementDto(
    long Id,
    long CompanyId,
    string CompanyName,
    string? CompanyLegalName,
    string? CompanyTradeName,
    string? CompanyRfc,
    string? ArchiveId,
    string Status,
    string? PitCode,
    string? CiaType,
    string? AgreementScope,
    string? Sector,
    string? BusinessLine,
    string? CompanySize,
    string? GeographicScope,
    string? Notes,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SaveCompanyAgreementDto(
    string? ArchiveId,
    string Status,
    string? PitCode,
    string? CiaType,
    string? AgreementScope,
    string? Sector,
    string? BusinessLine,
    string? CompanySize,
    string? GeographicScope,
    string? Notes
);
