using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Admin;

public class DashboardMetricsService : IDashboardMetricsService
{
    private readonly AppDbContext _context;

    public DashboardMetricsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DashboardMetricsResponseDto>> GetDashboardMetricsAsync()
    {
        var totalStudents = await _context.Students.CountAsync(s => s.IsActive);
        var activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive);
        var totalProjects = await _context.Projects.CountAsync(p => p.IsActive);
        var approvedProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Approved);
        var pendingProjects = await _context.Projects.CountAsync(p => p.IsActive && (p.Status == ProjectStatus.Pending || p.Status == ProjectStatus.Proposed || p.Status == ProjectStatus.UnderReview));
        
        // Count residencies that have a release letter
        var completedResidencies = await _context.Evaluations
            .Where(e => e.IsActive)
            .GroupBy(e => e.ProjectId)
            .Where(g => g.Average(e => e.Score) >= 70)
            .CountAsync();

        var metrics = new DashboardMetricsResponseDto(
            totalStudents,
            activeAdvisors,
            totalProjects,
            approvedProjects,
            pendingProjects,
            completedResidencies
        );

        return Result<DashboardMetricsResponseDto>.Success(metrics);
    }
}
