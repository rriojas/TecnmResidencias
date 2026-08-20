namespace TecNM.Residency.Companies;

public record CompanyResponseDto(
    long Id,
    string Name,
    string Rfc,
    string? Sector,
    string? Address,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    bool IsActive,
    bool IsVisible,
    int DisplayOrder,
    long? CreatedBy,
    long? UpdatedBy,
    long? DeletedBy,
    DateTime? DeletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
