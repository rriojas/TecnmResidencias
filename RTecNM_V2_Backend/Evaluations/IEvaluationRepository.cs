using TecNM.Residency.Common;

namespace TecNM.Residency.Evaluations;

public interface IEvaluationRepository
{
    Task<Evaluation> SaveEvaluationAsync(Evaluation evaluation);
    Task<Evaluation?> GetEvaluationByIdAsync(long id);
    Task<bool> SoftDeleteEvaluationAsync(long id, long? deletedBy);
    Task<PaginatedResult<Evaluation>> GetEvaluationsByProjectIdPagedAsync(long projectId, PaginationQuery query);
    Task<AdvisorySession> CreateAdvisorySessionAsync(AdvisorySession session);
    Task<AdvisorySession?> GetSessionByIdAsync(long id);
    Task UpdateSessionAsync(AdvisorySession session);
    Task<bool> SoftDeleteSessionAsync(long id, long? deletedBy);
    Task<PaginatedResult<AdvisorySession>> GetAdvisorySessionsByProjectIdPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false);
    Task<PaginatedResult<AdvisorySession>> GetAdvisorySessionsPagedAsync(PaginationQuery query, long? projectId, bool includeInactive = false);
    Task<List<AdvisorySession>> GetAllSessionsForExportAsync(long? projectId, string? search, string? sortBy, string? sortDir, bool includeInactive = false);
}
