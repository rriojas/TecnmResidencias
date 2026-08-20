using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

public interface IAdvisorRepository
{
    Task<PaginatedResult<Advisor>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<List<Advisor>> GetAllForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<List<Advisor>> GetOptionsAsync();
    Task<Advisor?> GetByIdAsync(long id);
    Task<Advisor?> GetByUserIdAsync(long userId);
    Task<Advisor> AddAsync(Advisor advisor);
    Task UpdateAsync(Advisor advisor);
}
