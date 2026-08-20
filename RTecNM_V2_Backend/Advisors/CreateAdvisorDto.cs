namespace TecNM.Residency.Advisors;

public record CreateAdvisorDto(
    long UserId,
    long DepartmentId,
    AdvisorType AdvisorType,
    string FullName,
    string? Title,
    string? Phone
);
