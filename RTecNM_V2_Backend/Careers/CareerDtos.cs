namespace TecNM.Residency.Careers;

public record CareerResponseDto(
    long Id,
    string Code,
    string Name,
    string Acronym,
    long? DepartmentId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateCareerDto(
    string Code,
    string Name,
    string Acronym,
    long? DepartmentId
);

public record UpdateCareerDto(
    string Code,
    string Name,
    string Acronym,
    long? DepartmentId
);
