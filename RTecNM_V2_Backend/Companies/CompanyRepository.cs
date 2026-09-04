using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public class CompanyRepository : ICompanyRepository
{
    private readonly AppDbContext _context;

    public CompanyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Company>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        IQueryable<Company> q = _context.Companies.AsNoTracking();

        if (status == "active")
            q = q.Where(c => c.IsActive);
        else if (status == "inactive")
            q = q.Where(c => !c.IsActive);
        else if (!includeInactive && status != "all")
            q = q.Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(c => c.Name.ToLower().Contains(term)
                             || (c.Rfc != null && c.Rfc.ToLower().Contains(term))
                             || (c.Sector != null && c.Sector.ToLower().Contains(term))
                             || (c.ContactName != null && c.ContactName.ToLower().Contains(term))
                             || (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Name", "Rfc", "Sector", "ContactName", "CreatedAt", "IsActive" },
            "Name", defaultDescending: false);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<IEnumerable<Company>> GetAllAsync(bool includeInactive = false)
    {
        var query = _context.Companies.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Company?> GetByIdAsync(long id)
    {
        return await _context.Companies
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Company?> GetByRfcAsync(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc)) return null;
        var cleanRfc = rfc.Trim().ToUpperInvariant();
        return await _context.Companies
            .FirstOrDefaultAsync(c => c.Rfc != null && c.Rfc.ToUpper() == cleanRfc);
    }

    public async Task<Company> AddAsync(Company company)
    {
        await _context.Companies.AddAsync(company);
        await _context.SaveChangesAsync();
        return company;
    }

    public async Task UpdateAsync(Company company)
    {
        _context.Companies.Update(company);
        await _context.SaveChangesAsync();
    }
}
