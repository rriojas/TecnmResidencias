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

    public async Task<PaginatedResult<Advisor>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        IQueryable<Advisor> q = _context.Advisors.Include(a => a.User).AsNoTracking()
            .Where(a => a.User == null || a.User.Role == UserRole.Advisor || a.User.Role == UserRole.Academic);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(a => a.DepartmentId == _currentUser.CareerId.Value);
        }

        if (status == "active")
            q = q.Where(a => a.IsActive);
        else if (status == "inactive" || includeInactive)
            q = q.Where(a => !a.IsActive);
        else
            q = q.Where(a => a.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(a => a.FullName.ToLower().Contains(term)
                             || (a.Title != null && a.Title.ToLower().Contains(term))
                             || (a.Phone != null && a.Phone.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "FullName", "Title", "AdvisorType", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Advisor>> GetAllForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<Advisor> q = _context.Advisors.Include(a => a.User).AsNoTracking()
            .Where(a => a.User == null || a.User.Role == UserRole.Advisor || a.User.Role == UserRole.Academic);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(a => a.DepartmentId == _currentUser.CareerId.Value);
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
            .AsNoTracking()
            .Where(a => a.User == null || a.User.Role == UserRole.Advisor || a.User.Role == UserRole.Academic);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(a => a.DepartmentId == _currentUser.CareerId.Value);
        }

        return await q.OrderBy(a => a.FullName)
            .ToListAsync();
    }

    public async Task<Advisor?> GetByIdAsync(long id)
    {
        var advisor = await _context.Advisors
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id && (a.User == null || a.User.Role == UserRole.Advisor || a.User.Role == UserRole.Academic));

        if (advisor != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && advisor.DepartmentId != _currentUser.CareerId.Value)
            return null;

        return advisor;
    }

    public async Task<Advisor?> GetByUserIdAsync(long userId)
    {
        var advisor = await _context.Advisors
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == userId && (a.User == null || a.User.Role == UserRole.Advisor || a.User.Role == UserRole.Academic));

        if (advisor != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && advisor.DepartmentId != _currentUser.CareerId.Value)
            return null;

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
