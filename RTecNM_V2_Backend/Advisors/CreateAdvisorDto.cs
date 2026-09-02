namespace TecNM.Residency.Advisors;

public record CreateAdvisorDto(
    string Email,
    string? Password,
    long DepartmentId,
    AdvisorType AdvisorType,
    string? FullName = null,
    string? FirstName = null,
    string? LastName = null,
    string? Title = null,
    string? Phone = null,
    long? UserId = null
);
