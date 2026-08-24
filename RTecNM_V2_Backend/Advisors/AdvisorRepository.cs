using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

public class AdvisorRepository : IAdvisorRepository
{
    private readonly AppDbContext _context;

    public AdvisorRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Advisor>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        IQueryable<Advisor> q = _context.Advisors.AsNoTracking();

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
        IQueryable<Advisor> q = _context.Advisors.AsNoTracking();

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
        return await _context.Advisors
            .AsNoTracking()
            .OrderBy(a => a.FullName)
            .ToListAsync();
    }

    public async Task<Advisor?> GetByIdAsync(long id)
    {
        return await _context.Advisors
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Advisor?> GetByUserIdAsync(long userId)
    {
        return await _context.Advisors
            .FirstOrDefaultAsync(a => a.UserId == userId);
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
