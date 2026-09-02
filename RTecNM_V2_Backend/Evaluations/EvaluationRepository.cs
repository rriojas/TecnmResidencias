using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Evaluations;

public class EvaluationRepository : IEvaluationRepository
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EvaluationRepository(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Evaluation> SaveEvaluationAsync(Evaluation evaluation)
    {
        var existing = await _context.Evaluations
            .FirstOrDefaultAsync(e => e.ProjectId == evaluation.ProjectId 
                                   && e.EvaluationPeriod == evaluation.EvaluationPeriod 
                                   && e.IsActive);

        if (existing != null)
        {
            existing.Score = evaluation.Score;
            existing.Feedback = evaluation.Feedback;
            existing.EvaluatorId = evaluation.EvaluatorId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = evaluation.UpdatedBy;
            _context.Evaluations.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        _context.Evaluations.Add(evaluation);
        await _context.SaveChangesAsync();
        return evaluation;
    }

    public async Task<Evaluation?> GetEvaluationByIdAsync(long id)
    {
        return await _context.Evaluations
            .Include(e => e.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<bool> SoftDeleteEvaluationAsync(long id, long? deletedBy)
    {
        var eval = await _context.Evaluations.FirstOrDefaultAsync(e => e.Id == id);
        if (eval == null || !eval.IsActive) return false;

        eval.IsActive = false;
        eval.DeletedAt = DateTime.UtcNow;
        eval.DeletedBy = deletedBy;
        eval.UpdatedAt = DateTime.UtcNow;
        eval.UpdatedBy = deletedBy;
        _context.Evaluations.Update(eval);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaginatedResult<Evaluation>> GetEvaluationsByProjectIdPagedAsync(long projectId, PaginationQuery query)
    {
        var q = _context.Evaluations
            .Include(e => e.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Where(e => e.ProjectId == projectId && e.IsActive)
            .OrderBy(e => e.CreatedAt);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<AdvisorySession> CreateAdvisorySessionAsync(AdvisorySession session)
    {
        _context.AdvisorySessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<AdvisorySession?> GetSessionByIdAsync(long id)
    {
        var session = await _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue
            && session.Project?.Student != null && session.Project.Student.CareerId != _currentUser.CareerId.Value)
            return null;

        return session;
    }

    public async Task UpdateSessionAsync(AdvisorySession session)
    {
        _context.AdvisorySessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> SoftDeleteSessionAsync(long id, long? deletedBy)
    {
        var session = await _context.AdvisorySessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session == null || !session.IsActive) return false;

        session.IsActive = false;
        session.DeletedAt = DateTime.UtcNow;
        session.DeletedBy = deletedBy;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = deletedBy;
        _context.AdvisorySessions.Update(session);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaginatedResult<AdvisorySession>> GetAdvisorySessionsByProjectIdPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor)
            .Where(s => s.ProjectId == projectId);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "SessionDate", "CreatedAt" },
            "SessionDate", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<PaginatedResult<AdvisorySession>> GetAdvisorySessionsPagedAsync(PaginationQuery query, long? projectId, bool includeInactive = false)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        if (projectId.HasValue)
            q = q.Where(s => s.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "SessionDate", "CreatedAt" },
            "SessionDate", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<AdvisorySession>> GetAllSessionsForExportAsync(long? projectId, string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor)
            .AsNoTracking();

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        if (projectId.HasValue)
            q = q.Where(s => s.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term)));
        }

        q = q.ApplySort(sortBy, sortDir,
            new[] { "SessionDate", "CreatedAt" },
            "SessionDate", defaultDescending: true);

        return await q.Take(1000).ToListAsync();
    }
}
