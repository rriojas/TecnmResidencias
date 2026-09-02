using TecNM.Residency.Common;

namespace TecNM.Residency.Careers;

public interface ICareerRepository
{
    Task<PaginatedResult<Career>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<List<Career>> GetAllAsync(bool includeInactive = false);
    Task<Career?> GetByIdAsync(long id);
    Task<Career?> GetByCodeAsync(string code);
    Task<Career> CreateAsync(Career career);
    Task<Career> UpdateAsync(Career career);
}
