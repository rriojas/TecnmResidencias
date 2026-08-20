namespace TecNM.Residency.Projects;

public record UpdateProjectStatusDto(
    string Status,
    string? Comments
);
