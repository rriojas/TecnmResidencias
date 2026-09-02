using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Careers;

public class CareerRepository : ICareerRepository
{
    private readonly AppDbContext _context;

    public CareerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Career>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        IQueryable<Career> q = _context.Careers.AsNoTracking();

        if (status == "active")
            q = q.Where(c => c.IsActive);
        else if (status == "inactive")
            q = q.Where(c => !c.IsActive);
        else if (!includeInactive && status != "all")
            q = q.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(c => c.Code.ToLower().Contains(term)
                             || c.Name.ToLower().Contains(term)
                             || c.Acronym.ToLower().Contains(term));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Code", "Name", "Acronym", "CreatedAt" },
            "Name", defaultDescending: false);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Career>> GetAllAsync(bool includeInactive = false)
    {
        IQueryable<Career> q = _context.Careers.AsNoTracking();

        if (!includeInactive)
            q = q.Where(c => c.IsActive);

        return await q.OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Career?> GetByIdAsync(long id)
    {
        return await _context.Careers.FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Career?> GetByCodeAsync(string code)
    {
        var cleanCode = code.Trim().ToUpperInvariant();
        return await _context.Careers.FirstOrDefaultAsync(c => c.Code.ToUpper() == cleanCode);
    }

    public async Task<Career> CreateAsync(Career career)
    {
        _context.Careers.Add(career);
        await _context.SaveChangesAsync();
        return career;
    }

    public async Task<Career> UpdateAsync(Career career)
    {
        _context.Careers.Update(career);
        await _context.SaveChangesAsync();
        return career;
    }
}
