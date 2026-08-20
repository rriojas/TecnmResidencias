namespace TecNM.Residency.Admin;

public record DashboardMetricsResponseDto(
    int TotalStudents,
    int ActiveAdvisors,
    int TotalProjects,
    int ApprovedProjects,
    int PendingProjects,
    int CompletedResidencies
);
