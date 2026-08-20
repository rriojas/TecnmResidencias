using TecNM.Residency.Common;

namespace TecNM.Residency.Projects;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(long id);
    Task<Project?> GetByStudentIdAsync(long studentId);
    Task<Project?> GetActiveByStudentIdAsync(long studentId, bool excludeDraft = false);
    Task<PaginatedResult<Project>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<List<Project>> GetAllForExportAsync(string? status, string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<PaginatedResult<Project>> GetPagedByStudentIdAsync(long studentId, PaginationQuery query, bool includeInactive = false);
    Task<PaginatedResult<Project>> GetPagedByAdvisorIdAsync(long advisorId, PaginationQuery query);
    Task<List<Project>> GetOptionsAsync(long? studentId, long? advisorId);
    Task<Project> CreateAsync(Project project);
    Task UpdateAsync(Project project);
    Task UpdateWithObjectivesAsync(Project project);
}
