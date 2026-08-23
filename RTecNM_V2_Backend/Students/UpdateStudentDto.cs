namespace TecNM.Residency.Students;

public class UpdateStudentDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? LastName2 { get; set; }
    public string? Curp { get; set; }
    public string? Gender { get; set; }
    public long CareerId { get; set; }
    public decimal Gpa { get; set; }
    public int? AcademicPeriodId { get; set; }
}
