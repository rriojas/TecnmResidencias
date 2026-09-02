namespace TecNM.Residency.Advisors;

public record UpdateAdvisorDto(
    long DepartmentId,
    AdvisorType AdvisorType,
    string? FullName = null,
    string? FirstName = null,
    string? LastName = null,
    string? Title = null,
    string? Phone = null
);
