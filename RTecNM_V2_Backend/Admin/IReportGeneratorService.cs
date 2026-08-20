using TecNM.Residency.Common;

namespace TecNM.Residency.Admin;

public interface IReportGeneratorService
{
    Task<Result<PaginatedResult<ReleasableProjectDto>>> GetReleasableProjectsAsync(PaginationQuery query);
    Task<Result<DocumentDto>> IssueReleaseLetterAsync(long projectId);
}
