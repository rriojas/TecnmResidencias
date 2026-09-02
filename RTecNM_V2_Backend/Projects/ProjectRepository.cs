using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Projects;

public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public ProjectRepository(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    private IQueryable<Project> QueryWithDetails() =>
        _context.Projects
            .Include(p => p.Objectives)
            .Include(p => p.Student)
                .ThenInclude(s => s!.User)
            .Include(p => p.Advisor)
            .Include(p => p.Company);

    public async Task<Project?> GetByIdAsync(long id)
    {
        var project = await QueryWithDetails()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && project.Student != null && project.Student.CareerId != _currentUser.CareerId.Value)
            return null;

        return project;
    }

    public async Task<Project?> GetByStudentIdAsync(long studentId)
    {
        return await QueryWithDetails()
            .FirstOrDefaultAsync(p => p.StudentId == studentId && p.IsActive);
    }

    public async Task<Project?> GetActiveByStudentIdAsync(long studentId, bool excludeDraft = false)
    {
        var activeStatuses = new[]
        {
            ProjectStatus.Draft,
            ProjectStatus.Pending,
            ProjectStatus.Proposed,
            ProjectStatus.UnderReview,
            ProjectStatus.Approved,
            ProjectStatus.InProgress
        }
        .Where(s => !excludeDraft || s != ProjectStatus.Draft)
        .ToArray();

        return await QueryWithDetails()
            .Where(p => p.StudentId == studentId && p.IsActive && activeStatuses.Contains(p.Status))
            .OrderByDescending(p => p.Status == ProjectStatus.InProgress)
            .ThenByDescending(p => p.Status == ProjectStatus.Approved)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<Project?> GetPrimaryProjectByStudentIdAsync(long studentId)
    {
        return await QueryWithDetails()
            .Where(p => p.StudentId == studentId && p.IsActive)
            .OrderByDescending(p => p.Status == ProjectStatus.InProgress)
            .ThenByDescending(p => p.Status == ProjectStatus.Approved)
            .ThenByDescending(p => p.Status == ProjectStatus.UnderReview || p.Status == ProjectStatus.Pending || p.Status == ProjectStatus.Proposed)
            .ThenByDescending(p => p.Status == ProjectStatus.Draft)
            .ThenByDescending(p => p.Status == ProjectStatus.Completed)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<PaginatedResult<Project>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        IQueryable<Project> q = QueryWithDetails();
        if (includeInactive)
            q = q.Where(p => !p.IsActive);
        else
            q = q.Where(p => p.IsActive);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(p => p.Student != null && p.Student.CareerId == _currentUser.CareerId.Value);
            q = q.Where(p => p.Status != ProjectStatus.Draft);
        }

        q = ApplyStatusFilter(q, status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(p => p.Title.ToLower().Contains(term)
                             || (p.Student != null && (p.Student.FirstName + " " + p.Student.LastName).ToLower().Contains(term))
                             || (p.Student != null && p.Student.ControlNumber.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Title", "Status", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Project>> GetAllForExportAsync(string? status, string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<Project> q = QueryWithDetails().AsNoTracking();
        if (!includeInactive)
            q = q.Where(p => p.IsActive);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(p => p.Student != null && p.Student.CareerId == _currentUser.CareerId.Value);
            q = q.Where(p => p.Status != ProjectStatus.Draft);
        }

        q = ApplyStatusFilter(q, status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(p => p.Title.ToLower().Contains(term)
                             || (p.Student != null && (p.Student.FirstName + " " + p.Student.LastName).ToLower().Contains(term))
                             || (p.Student != null && p.Student.ControlNumber.ToLower().Contains(term)));
        }

        q = q.ApplySort(sortBy, sortDir,
            new[] { "Title", "Status", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.Take(1000).ToListAsync();
    }

    public async Task<PaginatedResult<Project>> GetPagedByStudentIdAsync(long studentId, PaginationQuery query, bool includeInactive = false)
    {
        var q = QueryWithDetails()
            .Where(p => p.StudentId == studentId);
        if (!includeInactive)
            q = q.Where(p => p.IsActive);

        q = q.OrderByDescending(p => p.CreatedAt);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<PaginatedResult<Project>> GetPagedByAdvisorIdAsync(long advisorId, PaginationQuery query)
    {
        var q = QueryWithDetails()
            .Where(p => p.AdvisorId == advisorId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Project>> GetOptionsAsync(long? studentId, long? advisorId)
    {
        IQueryable<Project> q = _context.Projects.AsNoTracking()
            .Where(p => p.IsActive && p.Status != ProjectStatus.Draft);

        if (studentId.HasValue)
            q = q.Where(p => p.StudentId == studentId.Value);
        if (advisorId.HasValue)
            q = q.Where(p => p.AdvisorId == advisorId.Value);

        return await q
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    private static IQueryable<Project> ApplyStatusFilter(IQueryable<Project> q, string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
            return q;

        switch (status.ToLowerInvariant())
        {
            case "draft":
                return q.Where(p => p.Status == ProjectStatus.Draft);
            case "pending":
                return q.Where(p => p.Status == ProjectStatus.Pending
                                    || p.Status == ProjectStatus.Proposed
                                    || p.Status == ProjectStatus.UnderReview);
            case "approved":
                return q.Where(p => p.Status == ProjectStatus.Approved);
            case "rejected":
                return q.Where(p => p.Status == ProjectStatus.Rejected);
            default:
                if (Enum.TryParse<ProjectStatus>(status, true, out var parsed))
                    return q.Where(p => p.Status == parsed);
                return q;
        }
    }

    public async Task<Project> CreateAsync(Project project)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync();
        return project;
    }

    public async Task UpdateAsync(Project project)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateWithObjectivesAsync(Project project)
    {
        var oldObjectives = await _context.ProjectObjectives
            .Where(o => o.ProjectId == project.Id)
            .ToListAsync();
        _context.ProjectObjectives.RemoveRange(oldObjectives);

        _context.Projects.Update(project);
        await _context.SaveChangesAsync();
    }
}
