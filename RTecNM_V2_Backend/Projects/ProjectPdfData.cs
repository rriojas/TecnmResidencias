namespace TecNM.Residency.Projects;

public record ProjectPdfData(
    string StudentName,
    string CompanyName,
    string CompanyRfc,
    string? CompanySector,
    string? CompanyAddress,
    string CompanyContactName,
    string CompanyContactEmail,
    string? CompanyContactPhone,
    string AdvisorName,
    string Title,
    string? ProjectType,
    string ProblemStatement,
    string Justification,
    string GeneralObjective,
    List<string> SpecificObjectives
);
