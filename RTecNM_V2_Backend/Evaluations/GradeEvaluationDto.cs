namespace TecNM.Residency.Evaluations;

public record GradeEvaluationDto(
    long ProjectId,
    long EvaluatorId,
    string EvaluationPeriod,
    decimal Score,
    string? Feedback
);

public record CreateAdvisorySessionDto(
    long ProjectId,
    long AdvisorId,
    DateTime? SessionDate,
    string TopicsCovered,
    string? StudentAgreements
);

public record EvaluationResponseDto(
    long Id,
    long ProjectId,
    long EvaluatorId,
    string EvaluationPeriod,
    decimal Score,
    string? Feedback,
    DateTime CreatedAt,
    string ProjectTitle,
    string StudentName,
    bool IsVisible,
    int DisplayOrder,
    DateTime UpdatedAt,
    long? CreatedBy,
    long? UpdatedBy,
    long? DeletedBy,
    DateTime? DeletedAt
);

public record AdvisorySessionResponseDto(
    long Id,
    long ProjectId,
    long AdvisorId,
    DateTime SessionDate,
    string TopicsCovered,
    string? StudentAgreements,
    DateTime CreatedAt,
    string ProjectTitle,
    string StudentName,
    string AdvisorName,
    bool IsVisible,
    int DisplayOrder,
    DateTime UpdatedAt,
    long? CreatedBy,
    long? UpdatedBy,
    long? DeletedBy,
    DateTime? DeletedAt
);
