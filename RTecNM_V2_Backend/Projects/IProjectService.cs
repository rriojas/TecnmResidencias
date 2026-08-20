using TecNM.Residency.Common;

namespace TecNM.Residency.Projects;

public interface IProjectService
{
    Task<Result<ProjectResponseDto>> CreateProjectAsync(CreateProjectDto dto);
    Task<Result<ProjectResponseDto>> UpdateProjectAsync(long id, UpdateProjectDto dto);
    Task<Result<ProjectResponseDto>> SendToReviewAsync(long id);
    Task<Result<ProjectResponseDto>> GetProjectByIdAsync(long id);
    Task<Result<ProjectResponseDto>> GetProjectByStudentIdAsync(long studentId);
    Task<Result<ProjectResponseDto>> GetMyCurrentProjectAsync();
    Task<Result<PaginatedResult<ProjectResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<Result<byte[]>> ExportPdfAsync(string? status, string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<Result<byte[]>> GetProjectPdfAsync(long id);
    Task<Result<PaginatedResult<ProjectResponseDto>>> GetMyProjectsPagedAsync(PaginationQuery query, bool includeInactive = false);
    Task<Result<PaginatedResult<ProjectResponseDto>>> GetAdvisorProjectsPagedAsync(PaginationQuery query);
    Task<Result<PaginatedResult<ProjectResponseDto>>> GetProjectsByStudentIdPagedAsync(long studentId, PaginationQuery query);
    Task<Result<List<ProjectOptionDto>>> GetOptionsAsync();
    Task<Result<bool>> CanAccessProjectAsync(long projectId);
    Task<Result<ProjectResponseDto>> UpdateStatusAsync(long id, UpdateProjectStatusDto dto);
    Task<Result<ProjectResponseDto>> CancelProjectAsync(long id);
    Task<Result<bool>> SoftDeleteAsync(long id);
    Task<Result<bool>> ActivateAsync(long id);
}
