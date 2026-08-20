namespace TecNM.Residency.Admin;

public record DocumentDto(
    long Id,
    long ProjectId,
    string DocumentType,
    string DocumentName,
    string FilePath,
    string Status,
    DateTime IssuedAt
);

public record ReleasableProjectDto(
    long ProjectId,
    string Title,
    long StudentId,
    string StudentName,
    string StudentControlNumber,
    string AdvisorName,
    decimal AverageScore,
    bool IsEligible,
    bool IsReleased
);
