using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

public class AdvisorRepository : IAdvisorRepository
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public AdvisorRepository(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<Advisor>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false, long? departmentId = null)
    {
        IQueryable<Advisor> q = _context.Advisors.Include(a => a.User).AsNoTracking();

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(a => a.AdvisorType == AdvisorType.Internal);
        }

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            q = q.Where(a => a.DepartmentId == departmentId.Value);
        }

        if (status == "active")
            q = q.Where(a => a.IsActive);
        else if (status == "inactive")
            q = q.Where(a => !a.IsActive);
        else if (!includeInactive && status != "all")
            q = q.Where(a => a.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(a => a.FullName.ToLower().Contains(term)
                             || (a.Title != null && a.Title.ToLower().Contains(term))
                             || (a.Phone != null && a.Phone.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "FullName", "Title", "AdvisorType", "CreatedAt", "Phone", "IsActive", "DepartmentId" },
            "CreatedAt", defaultDescending: true);

        var result = await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        if (result.Items.Any())
        {
            var advisorIds = result.Items.Select(a => a.Id).ToList();
            var counts = await _context.Students
                .Where(s => s.AdvisorId.HasValue && advisorIds.Contains(s.AdvisorId.Value) && s.IsActive)
                .GroupBy(s => s.AdvisorId!.Value)
                .Select(g => new { AdvisorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AdvisorId, x => x.Count);

            foreach (var advisor in result.Items)
            {
                advisor.AssignedStudentsCount = counts.TryGetValue(advisor.Id, out var count) ? count : 0;
            }
        }

        return result;
    }

    public async Task<List<Advisor>> GetAllForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false, long? departmentId = null)
    {
        IQueryable<Advisor> q = _context.Advisors.Include(a => a.User).AsNoTracking();

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(a => a.AdvisorType == AdvisorType.Internal);
        }

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            q = q.Where(a => a.DepartmentId == departmentId.Value);
        }

        if (!includeInactive)
            q = q.Where(a => a.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(a => a.FullName.ToLower().Contains(term)
                             || (a.Title != null && a.Title.ToLower().Contains(term))
                             || (a.Phone != null && a.Phone.ToLower().Contains(term)));
        }

        q = q.ApplySort(sortBy, sortDir,
            new[] { "FullName", "Title", "AdvisorType", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.Take(1000).ToListAsync();
    }

    public async Task<List<Advisor>> GetOptionsAsync()
    {
        IQueryable<Advisor> q = _context.Advisors
            .Include(a => a.User)
            .AsNoTracking();

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(a => a.AdvisorType == AdvisorType.Internal);
        }

        var advisors = await q.OrderBy(a => a.FullName).ToListAsync();
        if (advisors.Count > 0)
        {
            var advisorIds = advisors.Select(a => a.Id).ToList();
            var counts = await _context.Students
                .Where(s => s.AdvisorId.HasValue && advisorIds.Contains(s.AdvisorId.Value) && s.IsActive)
                .GroupBy(s => s.AdvisorId!.Value)
                .Select(g => new { AdvisorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.AdvisorId, x => x.Count);

            foreach (var advisor in advisors)
            {
                advisor.AssignedStudentsCount = counts.TryGetValue(advisor.Id, out var count) ? count : 0;
            }
        }

        return advisors;
    }

    public async Task<Advisor?> GetByIdAsync(long id)
    {
        var advisor = await _context.Advisors
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (advisor != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            if (advisor.AdvisorType != AdvisorType.Internal) return null;
        }

        if (advisor != null)
        {
            advisor.AssignedStudentsCount = await _context.Students
                .CountAsync(s => s.AdvisorId == advisor.Id && s.IsActive);
        }

        return advisor;
    }

    public async Task<Advisor?> GetByUserIdAsync(long userId)
    {
        var advisor = await _context.Advisors
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (advisor != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            if (advisor.AdvisorType != AdvisorType.Internal) return null;
        }

        if (advisor != null)
        {
            advisor.AssignedStudentsCount = await _context.Students
                .CountAsync(s => s.AdvisorId == advisor.Id && s.IsActive);
        }

        return advisor;
    }

    public async Task<Advisor> AddAsync(Advisor advisor)
    {
        _context.Advisors.Add(advisor);
        await _context.SaveChangesAsync();
        return advisor;
    }

    public async Task UpdateAsync(Advisor advisor)
    {
        _context.Advisors.Update(advisor);
        await _context.SaveChangesAsync();
    }
}
