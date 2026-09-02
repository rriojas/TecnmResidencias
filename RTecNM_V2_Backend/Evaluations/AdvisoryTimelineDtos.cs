namespace TecNM.Residency.Evaluations;

public class AdvisoryTimelineQuery
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public long? AdvisorId { get; set; }
    public long? CareerId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? ObservationFilter { get; set; } // "all", "with_notes", "without_notes"
    public string? ProjectStatus { get; set; } // "all", "in_progress", "completed", etc.
    public string? HealthStatus { get; set; } // "all", "healthy", "warning", "critical", "irregular"
    public string? Search { get; set; }
    public string? SortBy { get; set; } = "SessionDate";
    public string? SortDir { get; set; } = "desc";
    public bool IncludeInactive { get; set; } = false;
}

public class AdvisoryTimelineItemDto
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public long AdvisorId { get; set; }
    public string AdvisorName { get; set; } = string.Empty;
    public string? AdvisorTitle { get; set; }
    public string? AdvisorEmail { get; set; }
    public long StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentControlNumber { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public string CareerName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public string TopicsCovered { get; set; } = string.Empty;
    public string? StudentAgreements { get; set; }
    public string? SupervisionNotes { get; set; }
    public DateTime? SupervisedAt { get; set; }
    public long? SupervisedBy { get; set; }
    public string? SupervisorName { get; set; }
    public bool HasSupervisionNote => !string.IsNullOrWhiteSpace(SupervisionNotes);
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class StudentAdvisoryDotDto
{
    public long Id { get; set; }
    public int SessionNumber { get; set; }
    public DateTime SessionDate { get; set; }
    public string TopicsCovered { get; set; } = string.Empty;
    public string? StudentAgreements { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? SupervisionNotes { get; set; }
    public DateTime? SupervisedAt { get; set; }
    public bool HasSupervisionNote => !string.IsNullOrWhiteSpace(SupervisionNotes);
}

public class AdvisorStudentTimelineDto
{
    public long StudentId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentControlNumber { get; set; } = string.Empty;
    public long? ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public DateTime? LastSessionDate { get; set; }
    public int DaysWithoutActivity { get; set; }
    public string HealthStatus { get; set; } = "healthy"; // healthy, warning, critical
    public string AlertMessage { get; set; } = string.Empty;
    public List<StudentAdvisoryDotDto> Sessions { get; set; } = new();
}

public class AdvisorHealthMetricDto
{
    public long AdvisorId { get; set; }
    public string AdvisorName { get; set; } = string.Empty;
    public string? AdvisorTitle { get; set; }
    public string? AdvisorEmail { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalAssignedResidents { get; set; }
    public int TotalSessions { get; set; }
    public DateTime? LastSessionDate { get; set; }
    public int DaysWithoutActivity { get; set; }
    public string HealthStatus { get; set; } = "healthy"; // healthy, warning, critical, irregular, unassigned
    public string HealthLabel { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
    public List<AdvisorStudentTimelineDto> Students { get; set; } = new();
}

public class AdvisoryTimelineSummaryDto
{
    public int TotalAdvisors { get; set; }
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
    public int IrregularCount { get; set; }
    public int TotalSessions { get; set; }
    public int ObservedSessionsCount { get; set; }
    public List<AdvisorHealthMetricDto> AdvisorHealthMetrics { get; set; } = new();
}

public class SaveSupervisionNoteDto
{
    public string? Notes { get; set; }
}
