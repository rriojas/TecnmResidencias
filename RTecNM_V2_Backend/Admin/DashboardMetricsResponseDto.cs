namespace TecNM.Residency.Admin;

public record DashboardMetricsResponseDto(
    int TotalStudents,
    int ActiveAdvisors,
    int TotalProjects,
    int ApprovedProjects,
    int PendingProjects,
    int CompletedResidencies,
    int ActiveCompanies = 0,
    int StudentsWithAdvisor = 0,
    int StudentsWithoutAdvisor = 0,
    int InProgressProjects = 0
);
