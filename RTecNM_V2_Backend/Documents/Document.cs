using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Documents;

public class Document : BaseEntity
{
    public long ProjectId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public string Status { get; set; } = DocumentStatus.Uploaded;
    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
}
