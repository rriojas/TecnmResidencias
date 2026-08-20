namespace TecNM.Residency.Projects;

public record UpdateProjectDto(
    string Title,
    string? ProjectType,
    string ProblemStatement,
    string Justification,
    string GeneralObjective,
    List<string>? SpecificObjectives
);
