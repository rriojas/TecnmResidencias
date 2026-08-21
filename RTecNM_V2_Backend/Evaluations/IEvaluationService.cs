using TecNM.Residency.Common;

namespace TecNM.Residency.Evaluations;

public interface IEvaluationService
{
    Task<Result<EvaluationResponseDto>> GradeEvaluationAsync(GradeEvaluationDto dto);
    Task<Result<bool>> DeleteEvaluationAsync(long id);
    Task<Result<PaginatedResult<EvaluationResponseDto>>> GetEvaluationsByProjectIdPagedAsync(long projectId, PaginationQuery query);
    Task<Result<AdvisorySessionResponseDto>> RecordAdvisorySessionAsync(CreateAdvisorySessionDto dto);
    Task<Result<AdvisorySessionResponseDto>> UpdateAdvisorySessionAsync(long id, UpdateAdvisorySessionDto dto);
    Task<Result<bool>> DeleteAdvisorySessionAsync(long id);
    Task<Result<PaginatedResult<AdvisorySessionResponseDto>>> GetAdvisorySessionsByProjectIdPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false);
    Task<Result<PaginatedResult<AdvisorySessionResponseDto>>> GetAllAdvisorySessionsPagedAsync(PaginationQuery query, long? projectId, bool includeInactive = false);
    Task<Result<byte[]>> ExportSessionsPdfAsync(long? projectId, string? search, string? sortBy, string? sortDir, bool includeInactive = false);
}
