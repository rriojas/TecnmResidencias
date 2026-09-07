using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public interface ICompanyRepository
{
    Task<PaginatedResult<Company>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false, bool onlyWithActiveAgreement = false);
    Task<IEnumerable<Company>> GetAllAsync(bool includeInactive = false, bool onlyWithActiveAgreement = false);
    Task<Company?> GetByIdAsync(long id);
    Task<Company?> GetByRfcAsync(string rfc);
    Task<Company?> GetByNameOrLegalNameAsync(string name);
    Task<Company> AddAsync(Company company);
    Task UpdateAsync(Company company);

    // Convenios N:M
    Task<CompanyAgreement?> GetAgreementByIdAsync(long agreementId);
    Task<CompanyAgreement?> GetAgreementByArchiveIdAsync(string archiveId);
    Task<List<CompanyAgreement>> GetAgreementsByCompanyIdAsync(long companyId);
    Task<PaginatedResult<CompanyAgreement>> GetAgreementsPagedAsync(PaginationQuery query, string? statusFilter);
    Task<CompanyAgreement> AddAgreementAsync(CompanyAgreement agreement, List<SaveAgreementCompanyItemDto> companies);
    Task UpdateAgreementAsync(CompanyAgreement agreement, List<SaveAgreementCompanyItemDto> companies);
    Task DeleteAgreementAsync(long agreementId);
    Task LinkCompanyToAgreementAsync(long agreementId, long companyId, string? scope);
}
