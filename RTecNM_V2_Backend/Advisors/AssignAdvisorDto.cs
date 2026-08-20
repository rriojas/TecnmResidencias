namespace TecNM.Residency.Advisors;

public record AssignAdvisorDto(
    long ProjectId,
    long AdvisorId,
    string AdvisorType
);
