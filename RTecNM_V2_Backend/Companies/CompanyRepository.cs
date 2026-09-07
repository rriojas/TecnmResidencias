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
        IQueryable<Company> q = _context.Companies
            .Include(c => c.Agreement)
            .AsNoTracking();

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
                             || (c.LegalName != null && c.LegalName.ToLower().Contains(term))
                             || (c.TradeName != null && c.TradeName.ToLower().Contains(term))
                             || (c.Rfc != null && c.Rfc.ToLower().Contains(term))
                             || (c.Sector != null && c.Sector.ToLower().Contains(term))
                             || (c.City != null && c.City.ToLower().Contains(term))
                             || (c.State != null && c.State.ToLower().Contains(term))
                             || (c.ContactName != null && c.ContactName.ToLower().Contains(term))
                             || (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Name", "LegalName", "TradeName", "Rfc", "Sector", "ContactName", "CreatedAt", "IsActive", "HasAgreement" },
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
            .Include(c => c.Agreement)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Company?> GetByRfcAsync(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc)) return null;
        var cleanRfc = rfc.Trim().ToUpperInvariant();
        return await _context.Companies
            .Include(c => c.Agreement)
            .FirstOrDefaultAsync(c => c.Rfc != null && c.Rfc.ToUpper() == cleanRfc);
    }

    public async Task<Company?> GetByNameOrLegalNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var clean = name.Trim().ToLowerInvariant();
        return await _context.Companies
            .Include(c => c.Agreement)
            .FirstOrDefaultAsync(c => c.Name.ToLower() == clean || 
                                     (c.LegalName != null && c.LegalName.ToLower() == clean) ||
                                     (c.TradeName != null && c.TradeName.ToLower() == clean));
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

    public async Task<CompanyAgreement?> GetAgreementByCompanyIdAsync(long companyId)
    {
        return await _context.CompanyAgreements
            .Include(ca => ca.Company)
            .FirstOrDefaultAsync(ca => ca.CompanyId == companyId);
    }

    public async Task<PaginatedResult<CompanyAgreement>> GetAgreementsPagedAsync(PaginationQuery query, string? statusFilter)
    {
        IQueryable<CompanyAgreement> q = _context.CompanyAgreements
            .Include(ca => ca.Company)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all")
        {
            var filter = statusFilter.Trim().ToUpperInvariant();
            q = q.Where(ca => ca.Status.ToUpper() == filter);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(ca => (ca.ArchiveId != null && ca.ArchiveId.ToLower().Contains(term))
                             || (ca.Company != null && ca.Company.Name.ToLower().Contains(term))
                             || (ca.Company != null && ca.Company.LegalName != null && ca.Company.LegalName.ToLower().Contains(term))
                             || (ca.Company != null && ca.Company.TradeName != null && ca.Company.TradeName.ToLower().Contains(term))
                             || (ca.Company != null && ca.Company.Rfc != null && ca.Company.Rfc.ToLower().Contains(term))
                             || (ca.PitCode != null && ca.PitCode.ToLower().Contains(term))
                             || (ca.CiaType != null && ca.CiaType.ToLower().Contains(term))
                             || (ca.Sector != null && ca.Sector.ToLower().Contains(term))
                             || (ca.BusinessLine != null && ca.BusinessLine.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Status", "ArchiveId", "PitCode", "CiaType", "Sector", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task SaveAgreementAsync(CompanyAgreement agreement)
    {
        if (agreement.Id == 0)
        {
            await _context.CompanyAgreements.AddAsync(agreement);
        }
        else
        {
            _context.CompanyAgreements.Update(agreement);
        }
        await _context.SaveChangesAsync();
    }
}
