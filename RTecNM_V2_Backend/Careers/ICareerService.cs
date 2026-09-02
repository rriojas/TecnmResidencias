using TecNM.Residency.Common;

namespace TecNM.Residency.Careers;

public interface ICareerService
{
    Task<Result<PaginatedResult<CareerResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<Result<List<CareerResponseDto>>> GetAllAsync(bool includeInactive = false);
    Task<Result<CareerResponseDto>> GetByIdAsync(long id);
    Task<Result<CareerResponseDto>> CreateAsync(CreateCareerDto dto);
    Task<Result<CareerResponseDto>> UpdateAsync(long id, UpdateCareerDto dto);
    Task<Result<CareerResponseDto>> ToggleStatusAsync(long id);
}
