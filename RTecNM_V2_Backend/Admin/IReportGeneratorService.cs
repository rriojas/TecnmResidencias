using TecNM.Residency.Common;

namespace TecNM.Residency.Admin;

public interface IReportGeneratorService
{
    Task<Result<PaginatedResult<ReleasableProjectDto>>> GetReleasableProjectsAsync(PaginationQuery query, long? careerId = null, long? advisorId = null);
    Task<Result<DocumentDto>> IssueReleaseLetterAsync(long projectId);
}
