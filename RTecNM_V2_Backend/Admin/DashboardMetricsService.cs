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

    public async Task<Result<DashboardMetricsResponseDto>> GetDashboardMetricsAsync(long? careerId = null)
    {
        int totalStudents;
        int activeAdvisors;
        int totalProjects;
        int approvedProjects;
        int pendingProjects;
        int completedResidencies;
        int activeCompanies = await _context.Companies.CountAsync(c => c.IsActive);

        if (careerId.HasValue && careerId.Value > 0)
        {
            var cid = careerId.Value;
            totalStudents = await _context.Students.CountAsync(s => s.IsActive && s.CareerId == cid);
            activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive && a.DepartmentId == cid);
            totalProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Student != null && p.Student.CareerId == cid);
            approvedProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Approved && p.Student != null && p.Student.CareerId == cid);
            pendingProjects = await _context.Projects.CountAsync(p => p.IsActive && (p.Status == ProjectStatus.Pending || p.Status == ProjectStatus.Proposed || p.Status == ProjectStatus.UnderReview) && p.Student != null && p.Student.CareerId == cid);
            completedResidencies = await _context.Evaluations
                .Where(e => e.IsActive && e.Project != null && e.Project.Student != null && e.Project.Student.CareerId == cid)
                .GroupBy(e => e.ProjectId)
                .Where(g => g.Average(e => e.Score) >= 70)
                .CountAsync();
        }
        else
        {
            totalStudents = await _context.Students.CountAsync(s => s.IsActive);
            activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive);
            totalProjects = await _context.Projects.CountAsync(p => p.IsActive);
            approvedProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Approved);
            pendingProjects = await _context.Projects.CountAsync(p => p.IsActive && (p.Status == ProjectStatus.Pending || p.Status == ProjectStatus.Proposed || p.Status == ProjectStatus.UnderReview));
            completedResidencies = await _context.Evaluations
                .Where(e => e.IsActive)
                .GroupBy(e => e.ProjectId)
                .Where(g => g.Average(e => e.Score) >= 70)
                .CountAsync();
        }

        var metrics = new DashboardMetricsResponseDto(
            totalStudents,
            activeAdvisors,
            totalProjects,
            approvedProjects,
            pendingProjects,
            completedResidencies,
            activeCompanies
        );

        return Result<DashboardMetricsResponseDto>.Success(metrics);
    }
}

