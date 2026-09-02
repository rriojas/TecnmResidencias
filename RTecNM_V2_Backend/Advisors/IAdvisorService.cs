using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

public interface IAdvisorService
{
    Task<Result<PaginatedResult<AdvisorResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<Result<byte[]>> ExportPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<Result<List<AdvisorOptionDto>>> GetOptionsAsync();
    Task<Result<AdvisorResponseDto>> GetAdvisorByIdAsync(long id);
    Task<Result<AdvisorResponseDto>> GetMeAsync(long userId);
    Task<Result<AdvisorResponseDto>> CreateAdvisorAsync(CreateAdvisorDto dto);
    Task<Result<AdvisorResponseDto>> UpdateAdvisorAsync(long id, UpdateAdvisorDto dto);
    Task<Result<bool>> SoftDeleteAdvisorAsync(long id, long deletedByUserId);
    Task<Result<bool>> ReactivateAdvisorAsync(long id);
    Task<Result<bool>> AssignAdvisorAsync(AssignAdvisorDto dto);
    Task<Result<AdvisorResidentsResponseDto>> GetAdvisorResidentsAsync(long id);
}
