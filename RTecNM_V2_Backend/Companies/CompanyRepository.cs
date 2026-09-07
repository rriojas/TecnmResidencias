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
            .Include(c => c.AgreementCompanies)
                .ThenInclude(ac => ac.Agreement)
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
            .Include(c => c.AgreementCompanies)
                .ThenInclude(ac => ac.Agreement)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Company?> GetByRfcAsync(string rfc)
    {
        if (string.IsNullOrWhiteSpace(rfc)) return null;
        var cleanRfc = rfc.Trim().ToUpperInvariant();
        return await _context.Companies
            .Include(c => c.AgreementCompanies)
                .ThenInclude(ac => ac.Agreement)
            .FirstOrDefaultAsync(c => c.Rfc != null && c.Rfc.ToUpper() == cleanRfc);
    }

    public async Task<Company?> GetByNameOrLegalNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var clean = name.Trim().ToLowerInvariant();
        return await _context.Companies
            .Include(c => c.AgreementCompanies)
                .ThenInclude(ac => ac.Agreement)
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

    public async Task<CompanyAgreement?> GetAgreementByIdAsync(long agreementId)
    {
        return await _context.CompanyAgreements
            .Include(ca => ca.AgreementCompanies)
                .ThenInclude(ac => ac.Company)
            .FirstOrDefaultAsync(ca => ca.Id == agreementId);
    }

    public async Task<List<CompanyAgreement>> GetAgreementsByCompanyIdAsync(long companyId)
    {
        return await _context.AgreementCompanies
            .Where(ac => ac.CompanyId == companyId)
            .Include(ac => ac.Agreement)
            .Select(ac => ac.Agreement!)
            .Where(a => a != null)
            .ToListAsync();
    }

    public async Task<PaginatedResult<CompanyAgreement>> GetAgreementsPagedAsync(PaginationQuery query, string? statusFilter)
    {
        IQueryable<CompanyAgreement> q = _context.CompanyAgreements
            .Include(ca => ca.AgreementCompanies)
                .ThenInclude(ac => ac.Company)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all")
        {
            var filter = statusFilter.Trim().ToUpperInvariant();
            q = q.Where(ca => ca.Status.ToUpper().Contains(filter));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(ca => (ca.ArchiveId != null && ca.ArchiveId.ToLower().Contains(term))
                             || (ca.PitCode != null && ca.PitCode.ToLower().Contains(term))
                             || (ca.CiaType != null && ca.CiaType.ToLower().Contains(term))
                             || (ca.Sector != null && ca.Sector.ToLower().Contains(term))
                             || (ca.BusinessLine != null && ca.BusinessLine.ToLower().Contains(term))
                             || ca.AgreementCompanies.Any(ac => ac.Company != null && 
                                 (ac.Company.Name.ToLower().Contains(term) ||
                                  (ac.Company.LegalName != null && ac.Company.LegalName.ToLower().Contains(term)) ||
                                  (ac.Company.TradeName != null && ac.Company.TradeName.ToLower().Contains(term)) ||
                                  (ac.Company.Rfc != null && ac.Company.Rfc.ToLower().Contains(term)))));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Status", "ArchiveId", "PitCode", "CiaType", "Sector", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<CompanyAgreement> AddAgreementAsync(CompanyAgreement agreement, List<long> companyIds)
    {
        await _context.CompanyAgreements.AddAsync(agreement);
        await _context.SaveChangesAsync();

        if (companyIds.Count > 0)
        {
            foreach (var cid in companyIds.Distinct())
            {
                await _context.AgreementCompanies.AddAsync(new AgreementCompany
                {
                    AgreementId = agreement.Id,
                    CompanyId = cid,
                    CreatedAt = DateTime.UtcNow
                });

                var comp = await _context.Companies.FindAsync(cid);
                if (comp != null) comp.HasAgreement = true;
            }
            await _context.SaveChangesAsync();
        }

        return agreement;
    }

    public async Task UpdateAgreementAsync(CompanyAgreement agreement, List<long> companyIds)
    {
        _context.CompanyAgreements.Update(agreement);

        var existingLinks = await _context.AgreementCompanies
            .Where(ac => ac.AgreementId == agreement.Id)
            .ToListAsync();

        var existingCompanyIds = existingLinks.Select(l => l.CompanyId).ToHashSet();
        var newCompanyIds = companyIds.Distinct().ToHashSet();

        var toRemove = existingLinks.Where(l => !newCompanyIds.Contains(l.CompanyId)).ToList();
        if (toRemove.Count > 0)
        {
            _context.AgreementCompanies.RemoveRange(toRemove);
        }

        var toAdd = newCompanyIds.Where(id => !existingCompanyIds.Contains(id)).ToList();
        foreach (var cid in toAdd)
        {
            await _context.AgreementCompanies.AddAsync(new AgreementCompany
            {
                AgreementId = agreement.Id,
                CompanyId = cid,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        // Actualizar bandera HasAgreement en empresas afectadas
        var allAffectedIds = existingCompanyIds.Concat(newCompanyIds).Distinct();
        foreach (var cid in allAffectedIds)
        {
            var hasActive = await _context.AgreementCompanies
                .AnyAsync(ac => ac.CompanyId == cid && ac.Agreement != null && ac.Agreement.IsActive);
            var comp = await _context.Companies.FindAsync(cid);
            if (comp != null) comp.HasAgreement = hasActive;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAgreementAsync(long agreementId)
    {
        var agreement = await _context.CompanyAgreements.FindAsync(agreementId);
        if (agreement != null)
        {
            agreement.IsActive = false;
            await _context.SaveChangesAsync();
        }
    }
}
