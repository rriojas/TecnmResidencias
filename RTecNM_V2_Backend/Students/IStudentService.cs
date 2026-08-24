using TecNM.Residency.Common;

namespace TecNM.Residency.Students;

public interface IStudentService
{
    Task<Result<PaginatedResult<StudentResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<Result<byte[]>> ExportPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<Result<List<StudentOptionDto>>> GetOptionsAsync();
    Task<Result<StudentResponseDto>> GetByIdAsync(long id);
    Task<Result<StudentResponseDto>> GetMeAsync(long userId);
    Task<Result<StudentResponseDto>> CreateAsync(CreateStudentDto dto);
    Task<Result<StudentResponseDto>> UpdateAsync(long id, UpdateStudentDto dto);
    Task<Result<StudentResponseDto>> AssignAdvisorAsync(long studentId, long advisorId);
    Task<Result<bool>> SoftDeleteAsync(long id, long deletedByUserId);
    Task<Result<bool>> ReactivateAsync(long id);
    Task<Result<BatchImportResultDto>> ImportExcelAsync(Microsoft.AspNetCore.Http.IFormFile file);
    Task<Result<int>> SendMassPresentationLettersAsync();
    Task<Result<bool>> SendPresentationLetterAsync(long studentId);
    Task<Result<byte[]>> GetPresentationLetterPdfAsync(long studentId);
}
