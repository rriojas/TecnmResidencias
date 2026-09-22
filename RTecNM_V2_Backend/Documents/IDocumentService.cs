using TecNM.Residency.Common;

namespace TecNM.Residency.Documents;

public interface IDocumentService
{
    Task<DocumentResponseDto> UploadDocumentAsync(UploadDocumentDto dto, string webRootPath);
    Task<Result<PaginatedResult<DocumentResponseDto>>> GetByProjectPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false);
    Task<(byte[] FileBytes, string ContentType, string FileName)> DownloadDocumentAsync(long id, string webRootPath);
    Task<DocumentResponseDto?> UpdateStatusAsync(long id, UpdateDocumentStatusDto dto);
    Task<bool> SoftDeleteAsync(long id);
    Task<bool> ActivateAsync(long id);
    Task<List<PendingAcceptanceDto>> GetPendingAcceptanceLettersAsync(long? careerId = null);
    Task<PaginatedResult<DocumentMatrixItemDto>> GetDocumentMatrixAsync(PaginationQuery query, long? careerId = null, string? completionStatus = null);
}
