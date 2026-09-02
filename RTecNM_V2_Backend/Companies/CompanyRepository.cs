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
        var cleanRfc = (rfc ?? "").Trim().ToUpperInvariant();
        return await _context.Companies
            .FirstOrDefaultAsync(c => c.Rfc.ToUpper() == cleanRfc);
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
