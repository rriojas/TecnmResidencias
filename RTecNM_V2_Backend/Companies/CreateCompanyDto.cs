namespace TecNM.Residency.Companies;

public record CreateCompanyDto(
    string Name,
    string? Rfc,
    string? Sector,
    string? Address,
    string ContactName,
    string ContactEmail,
    string? ContactPhone
);
