namespace TecNM.Residency.Documents;

public class DocumentFileSummaryDto
{
    public long Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class DocumentMatrixItemDto
{
    public long ProjectId { get; set; }
    public long StudentId { get; set; }
    public string StudentControlNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public string CareerName { get; set; } = string.Empty;
    public string ProjectTitle { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string ProjectStatus { get; set; } = string.Empty;
    public Dictionary<string, DocumentFileSummaryDto> Documents { get; set; } = new();
    public int UploadedCount { get; set; }
    public int RequiredCount { get; set; }
    public bool IsCompleted { get; set; }
}
