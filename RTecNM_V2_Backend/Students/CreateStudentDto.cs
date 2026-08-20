namespace TecNM.Residency.Students;

public class CreateStudentDto
{
    public string ControlNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public decimal Gpa { get; set; }
    public int? AcademicPeriodId { get; set; }
}
