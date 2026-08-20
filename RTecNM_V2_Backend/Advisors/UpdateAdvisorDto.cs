namespace TecNM.Residency.Advisors;

public record UpdateAdvisorDto(
    long DepartmentId,
    AdvisorType AdvisorType,
    string FullName,
    string? Title,
    string? Phone
);
