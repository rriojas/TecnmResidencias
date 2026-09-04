using TecNM.Residency.Common;

namespace TecNM.Residency.Companies;

public interface ICompanyService
{
    Task<Result<PaginatedResult<CompanyResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<Result<IEnumerable<CompanyResponseDto>>> GetAllAsync(bool includeInactive = false);
    Task<Result<CompanyResponseDto>> GetByIdAsync(long id);
    Task<Result<CompanyResponseDto>> CreateAsync(CreateCompanyDto dto, long? createdByUserId = null);
    Task<Result<CompanyResponseDto>> UpdateAsync(long id, UpdateCompanyDto dto, long? updatedByUserId = null);
    Task<Result<bool>> SoftDeleteAsync(long id, long deletedByUserId);
    Task<Result<bool>> ReactivateAsync(long id);
    Task<Result<BatchImportResultDto>> ImportExcelAsync(Microsoft.AspNetCore.Http.IFormFile file, long? createdByUserId = null);
}
