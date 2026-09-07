namespace TecNM.Residency.Companies;

public record AgreementCompanyItemDto(
    long CompanyId,
    string CompanyName,
    string? LegalName,
    string? TradeName,
    string? Rfc,
    string? Sector,
    string? AgreementScope
);

public record CompanyAgreementDto(
    long Id,
    string? ArchiveId,
    string Status,
    DateTime? ExpirationDate,
    string? ExpirationDateFormatted,
    string? ProcessStatus,
    string? PitCode,
    string? CiaType,
    List<AgreementCompanyItemDto> Companies,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SaveAgreementCompanyItemDto(
    long CompanyId,
    string? AgreementScope
);

public record SaveCompanyAgreementDto(
    string? ArchiveId,
    DateTime? ExpirationDate,
    string? ProcessStatus,
    string? PitCode,
    string? CiaType,
    List<SaveAgreementCompanyItemDto>? Companies
);
