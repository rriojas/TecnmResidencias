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
        int eligibleStudentsForAdvisor = 0;
        int activeCompanies = await _context.Companies.CountAsync(c => c.IsActive);

        if (careerId.HasValue && careerId.Value > 0)
        {
            var cid = careerId.Value;
            totalStudents = await _context.Students.CountAsync(s => s.IsActive && s.CareerId == cid);
            studentsWithAdvisor = await _context.Students.CountAsync(s => s.IsActive && s.CareerId == cid && s.AdvisorId != null);
            
            // Alumnos elegibles pendientes de asignar: tienen anteproyecto activo (no borrador, no cancelado),
            // con carta de aceptación o InnovaTecNM/HackaTecNM, pero aún sin asesor asignado.
            studentsWithoutAdvisor = await _context.Students.CountAsync(s => s.IsActive && s.CareerId == cid && s.AdvisorId == null &&
                _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.Status != ProjectStatus.Draft && p.Status != ProjectStatus.Cancelled &&
                    (_context.Documents.Any(d => d.ProjectId == p.Id && d.IsActive &&
                        (d.DocumentType == "CartaAceptacion" || d.DocumentType == "carta_aceptacion" ||
                         d.DocumentType == "CartaAprobacion" || d.DocumentType == "carta_aprobacion" ||
                         d.DocumentType == "ConstanciaAcreditacion" || d.DocumentType == "constancia_acreditacion")) ||
                     (p.ProjectType != null && (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec") || p.ProjectType.ToLower().StartsWith("acreditacion"))) ||
                     (p.Title != null && (p.Title.ToLower().Contains("innovatec") || p.Title.ToLower().Contains("hackatec"))))));

            eligibleStudentsForAdvisor = studentsWithAdvisor + studentsWithoutAdvisor;
            activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive && a.DepartmentId == cid);

            // Para Jefe de Carrera: se excluyen los borradores y cancelados del conteo de anteproyectos
            totalProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status != ProjectStatus.Draft && p.Status != ProjectStatus.Cancelled && p.Student != null && p.Student.CareerId == cid);
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
            studentsWithoutAdvisor = await _context.Students.CountAsync(s => s.IsActive && s.AdvisorId == null &&
                _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.Status != ProjectStatus.Draft && p.Status != ProjectStatus.Cancelled &&
                    (_context.Documents.Any(d => d.ProjectId == p.Id && d.IsActive &&
                        (d.DocumentType == "CartaAceptacion" || d.DocumentType == "carta_aceptacion" ||
                         d.DocumentType == "CartaAprobacion" || d.DocumentType == "carta_aprobacion" ||
                         d.DocumentType == "ConstanciaAcreditacion" || d.DocumentType == "constancia_acreditacion")) ||
                     (p.ProjectType != null && (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec") || p.ProjectType.ToLower().StartsWith("acreditacion"))) ||
                     (p.Title != null && (p.Title.ToLower().Contains("innovatec") || p.Title.ToLower().Contains("hackatec"))))));

            eligibleStudentsForAdvisor = studentsWithAdvisor + studentsWithoutAdvisor;
            activeAdvisors = await _context.Advisors.CountAsync(a => a.IsActive);
            totalProjects = await _context.Projects.CountAsync(p => p.IsActive && p.Status != ProjectStatus.Draft && p.Status != ProjectStatus.Cancelled);
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
            inProgressProjects,
            eligibleStudentsForAdvisor
        );

        return Result<DashboardMetricsResponseDto>.Success(metrics);
    }
}

