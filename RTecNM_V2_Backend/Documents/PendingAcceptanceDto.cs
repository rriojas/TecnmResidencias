namespace TecNM.Residency.Documents;

public class PendingAcceptanceDto
{
    public long StudentId { get; set; }
    public string StudentControlNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public string CareerName { get; set; } = string.Empty;
    public long ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string ProjectStatus { get; set; } = string.Empty;
    public bool HasAcceptanceLetter { get; set; } = false;
    public string StatusLabel { get; set; } = "Sin carta de aceptación";
    public DateTime? ProjectCreatedAt { get; set; }
}
