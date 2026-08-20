namespace TecNM.Residency.Advisors;

public record AdvisorResponseDto(
    long Id,
    long UserId,
    long DepartmentId,
    string AdvisorType,
    string FullName,
    string? Title,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsVisible,
    int DisplayOrder,
    long? CreatedBy,
    long? UpdatedBy,
    long? DeletedBy,
    DateTime? DeletedAt
);
