namespace TecNM.Residency.Students;

public class UpdateStudentDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public long CareerId { get; set; }
    public decimal Gpa { get; set; }
}
