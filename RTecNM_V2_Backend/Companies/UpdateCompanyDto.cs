namespace TecNM.Residency.Companies;

public record UpdateCompanyDto(
    string Name,
    string? LegalName,
    string? TradeName,
    string? Rfc,
    string? Sector,
    string? Address,
    string? Street,
    string? Number,
    string? Colonia,
    string? City,
    string? State,
    string? PostalCode,
    string ContactName,
    string ContactEmail,
    string? ContactPhone
);
