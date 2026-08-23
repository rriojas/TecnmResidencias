using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Admin;

public class ReportGeneratorService : IReportGeneratorService
{
    private readonly AppDbContext _context;

    public ReportGeneratorService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedResult<ReleasableProjectDto>>> GetReleasableProjectsAsync(PaginationQuery query)
    {
        var projects = await _context.Projects
            .Include(p => p.Student)
                .ThenInclude(s => s!.User)
            .Include(p => p.Advisor)
            .Where(p => p.IsActive)
            .ToListAsync();

        // The total number of evaluation periods is always 3: partial_1, partial_2, final.
        // The average must be calculated over 3 regardless of how many have been graded.
        const int totalPeriods = 3;

        var scores = await _context.Evaluations
            .Where(e => e.IsActive)
            .GroupBy(e => e.ProjectId)
            .Select(g => new { ProjectId = g.Key, TotalScore = g.Sum(e => e.Score) })
            .ToListAsync();

        var scoreMap = scores.ToDictionary(s => s.ProjectId, s => s.TotalScore / totalPeriods);

        var list = projects.Select(p =>
        {
            var avgScore = scoreMap.TryGetValue(p.Id, out var avg) ? avg : 0m;
            bool isEligible = avg >= 70m;

            var studentName = p.Student is null
                ? $"Estudiante #{p.StudentId}"
                : $"{p.Student.FirstName} {p.Student.LastName}".Trim();
            var controlNumber = p.Student?.ControlNumber ?? string.Empty;
            var advisorName = p.Advisor?.FullName ?? string.Empty;

            return new ReleasableProjectDto(
                p.Id,
                p.Title,
                p.StudentId,
                studentName,
                controlNumber,
                advisorName,
                Math.Round(avgScore, 2),
                isEligible,
                false
            );
        }).ToList();

        var totalCount = list.Count;
        var items = list
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var result = PaginatedResult<ReleasableProjectDto>.Create(items, totalCount, query.PageNumber, query.PageSize);
        return Result<PaginatedResult<ReleasableProjectDto>>.Success(result);
    }

    public async Task<Result<DocumentDto>> IssueReleaseLetterAsync(long projectId)
    {
        var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.IsActive);
        if (project == null)
            return Result<DocumentDto>.Failure("Anteproyecto no encontrado.", 404);

        var evals = await _context.Evaluations
            .Where(e => e.ProjectId == projectId && e.IsActive)
            .ToListAsync();

        if (evals.Count == 0)
            return Result<DocumentDto>.Failure("El proyecto no cuenta con evaluaciones registradas.");

        // Always divide by 3 total periods (partial_1, partial_2, final).
        // Unregistered periods count as 0.
        const int totalPeriods = 3;
        decimal avgScore = evals.Sum(e => e.Score) / totalPeriods;
        if (avgScore < 70m)
            return Result<DocumentDto>.Failure($"La residencia no cumple con el puntaje mínimo de liberación (Promedio: {avgScore:F2} / 100, Mínimo: 70.00).");

        project.Status = TecNM.Residency.Projects.ProjectStatus.Completed;
        project.UpdatedAt = DateTime.UtcNow;
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();

        var doc = new DocumentDto(
            DateTime.UtcNow.Ticks,
            projectId,
            "release_letter",
            $"Carta_Liberacion_Proyecto_{projectId}.pdf",
            $"/documents/release_letter_{projectId}.pdf",
            "approved",
            DateTime.UtcNow
        );

        return Result<DocumentDto>.Success(doc);
    }
}
