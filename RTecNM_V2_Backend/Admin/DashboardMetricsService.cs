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
        int studentsWithAdvisor = 0;
        int studentsWithoutAdvisor = 0;
        int inProgressProjects = 0;
        int activeCompanies = await _context.Companies.CountAsync(c => c.IsActive);

        if (careerId.HasValue && careerId.Value > 0)
        {
            var cid = careerId.Value;
            totalStudents = await _context.Students.CountAsync(s => s.IsActive && s.CareerId == cid);
            studentsWithAdvisor = await _context.Students.CountAsync(s => s.IsActive && s.CareerId == cid && s.AdvisorId != null);
            studentsWithoutAdvisor = Math.Max(0, totalStudents - studentsWithAdvisor);
            activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive && a.DepartmentId == cid);

            // Para Jefe de Carrera: se excluyen los borradores del conteo de anteproyectos
            totalProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status != ProjectStatus.Draft && p.Student != null && p.Student.CareerId == cid);
            approvedProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Approved && p.Student != null && p.Student.CareerId == cid);
            inProgressProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.InProgress && p.Student != null && p.Student.CareerId == cid);
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
            studentsWithAdvisor = await _context.Students.CountAsync(s => s.IsActive && s.AdvisorId != null);
            studentsWithoutAdvisor = Math.Max(0, totalStudents - studentsWithAdvisor);
            activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive);
            totalProjects = await _context.Projects.CountAsync(p => p.IsActive);
            approvedProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.Approved);
            inProgressProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status == ProjectStatus.InProgress);
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
            activeCompanies,
            studentsWithAdvisor,
            studentsWithoutAdvisor,
            inProgressProjects
        );

        return Result<DashboardMetricsResponseDto>.Success(metrics);
    }
}

