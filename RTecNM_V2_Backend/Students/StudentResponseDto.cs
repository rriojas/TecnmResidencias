namespace TecNM.Residency.Students;

public class StudentResponseDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? LastName2 { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Curp { get; set; }
    public string? Gender { get; set; }
    public long CareerId { get; set; }
    public string? CareerName { get; set; }
    public long? AdvisorId { get; set; }
    public DateTime? AdvisorAssignedAt { get; set; }
    public string? AdvisorName { get; set; }
    public int? AcademicPeriodId { get; set; }
    public string Email { get; set; } = string.Empty;
    public decimal Gpa { get; set; }
    public bool IsPresentationLetterSent { get; set; }
    public DateTime? PresentationLetterSentAt { get; set; }

    public bool HasProject { get; set; }
    public long? ProjectId { get; set; }
    public bool HasAcceptanceLetter { get; set; }
    public string? AcceptanceLetterStatus { get; set; }
    public string? ProjectTitle { get; set; }
    public string? ProjectType { get; set; }
    public bool IsAccreditation { get; set; }
    public string? ExemptionReason { get; set; }
    public string? ProjectStatus { get; set; }
    public string ResidencyStage { get; set; } = "Sin Anteproyecto";

    public bool HasComplementaryActivities { get; set; }
    public bool HasSocialService { get; set; }
    public bool HasSpecialRequirements { get; set; }

    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }

    public DateTime? Formato29Deadline { get; set; }
    public DateTime? Formato30Deadline { get; set; }
    public string? Formato29Status { get; set; }
    public string? Formato29V2Status { get; set; }
    public string? Formato30Status { get; set; }
    public bool CanUploadSecondPhase { get; set; }
    public bool IsDocumentBlocked { get; set; }

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
