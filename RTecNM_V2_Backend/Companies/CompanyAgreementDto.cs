namespace TecNM.Residency.Companies;

public record CompanyBriefDto(
    long Id,
    string Name,
    string? LegalName,
    string? TradeName,
    string? Rfc
);

public record CompanyAgreementDto(
    long Id,
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
    List<CompanyBriefDto> Companies,
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
    string? Notes,
    List<long>? CompanyIds
);
