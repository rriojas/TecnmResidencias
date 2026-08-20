namespace TecNM.Residency.Activities;

public record WeeklyProgressDto(
    long Id,
    int WeekNumber,
    string Status,
    string? Notes
);

public record WeeklyActivityDto(
    long Id,
    long ProjectId,
    int ActivityNumber,
    string Title,
    List<WeeklyProgressDto> Progresses
);

public record SaveWeeklyProgressDto(
    long ActivityId,
    int WeekNumber,
    string Status,
    string? Notes
);

public record CreateActivityDto(
    long ProjectId,
    int ActivityNumber,
    string Title
);
