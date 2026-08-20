using Microsoft.AspNetCore.Http;

namespace TecNM.Residency.Documents;

public class UploadDocumentDto
{
    public long ProjectId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public IFormFile File { get; set; } = default!;
}

public class UpdateDocumentStatusDto
{
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
}

public class DocumentResponseDto
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public long? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
}
