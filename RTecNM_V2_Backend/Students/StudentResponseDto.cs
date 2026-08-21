namespace TecNM.Residency.Students;

public class StudentResponseDto
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public long? AdvisorId { get; set; }
    public string? AdvisorName { get; set; }
    public string Email { get; set; } = string.Empty;
    public decimal Gpa { get; set; }
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
