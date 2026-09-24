namespace TecNM.Residency.Students;

public class StudentDeadlineInfoDto
{
    public long StudentId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string LastName2 { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime? Formato29Deadline { get; set; }
    public DateTime? Formato30Deadline { get; set; }

    public string Formato29Status { get; set; } = "not_uploaded";
    public string? Formato29RejectionReason { get; set; }
    public string Formato29V2Status { get; set; } = "not_uploaded";
    public string? Formato29V2RejectionReason { get; set; }
    public string Formato30Status { get; set; } = "not_uploaded";
    public string? Formato30RejectionReason { get; set; }

    public bool CanUploadSecondPhase { get; set; } // Formato 29 approved
    public bool HasApprovedProject { get; set; }
    public bool HasApprovedAcceptanceLetter { get; set; }
    public bool IsAccreditation { get; set; }
    public bool IsEligibleForFormatDeadlines { get; set; }
    public bool IsDocumentBlocked { get; set; }
    public string? BlockedReason { get; set; }
}