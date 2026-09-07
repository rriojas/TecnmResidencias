using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public interface ICompanyRepository
{
    Task<PaginatedResult<Company>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<IEnumerable<Company>> GetAllAsync(bool includeInactive = false);
    Task<Company?> GetByIdAsync(long id);
    Task<Company?> GetByRfcAsync(string rfc);
    Task<Company?> GetByNameOrLegalNameAsync(string name);
    Task<Company> AddAsync(Company company);
    Task UpdateAsync(Company company);

    // Agreements
    Task<CompanyAgreement?> GetAgreementByCompanyIdAsync(long companyId);
    Task<PaginatedResult<CompanyAgreement>> GetAgreementsPagedAsync(PaginationQuery query, string? statusFilter);
    Task SaveAgreementAsync(CompanyAgreement agreement);
}
