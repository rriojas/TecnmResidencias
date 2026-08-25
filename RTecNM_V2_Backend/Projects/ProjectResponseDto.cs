namespace TecNM.Residency.Projects;

public record ProjectObjectiveDto(
    long Id,
    int ObjectiveNumber,
    string Description,
    string Status,
    string? Notes
);

public record ProjectResponseDto(
    long Id,
    long StudentId,
    long? AdvisorId,
    long CompanyId,
    string CompanyName,
    string Title,
    string? ProjectType,
    string ProblemStatement,
    string Justification,
    string GeneralObjective,
    string Status,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    DateTime CreatedAt,
    List<ProjectObjectiveDto> Objectives,
    string StudentName,
    string StudentControlNumber,
    string StudentEmail,
    string AdvisorName,
    bool IsVisible,
    int DisplayOrder,
    DateTime UpdatedAt,
    long? CreatedBy,
    long? UpdatedBy,
    long? DeletedBy,
    DateTime? DeletedAt,
    bool IsCompleted = false,
    bool IsReadOnly = false,
    bool CanManageActivities = false,
    bool CanUploadDocuments = false,
    string? ReviewComments = null,
    long? CareerId = null,
    string? CareerName = null
);
