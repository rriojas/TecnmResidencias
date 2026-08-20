namespace TecNM.Residency.Projects;

public record CreateProjectDto(
    long? StudentId,
    long? AdvisorId,
    string Title,
    string? ProjectType,
    string ProblemStatement,
    string Justification,
    string GeneralObjective,
    List<string>? SpecificObjectives,
    long CompanyId
);
