namespace TecNM.Residency.Students;

public class CreateStudentDto
{
    public string ControlNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? LastName2 { get; set; }
    public string? Curp { get; set; }
    public string? Gender { get; set; }
    public long CareerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public decimal Gpa { get; set; }
    public int? AcademicPeriodId { get; set; }
}
