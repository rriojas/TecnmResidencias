using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Students;

public class Student : BaseEntity
{
    public long UserId { get; set; }
    public string ControlNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty; // Maps to last_name_1
    public string? LastName2 { get; set; }               // Maps to last_name_2
    public string? Curp { get; set; }                    // Maps to curp
    public string? Gender { get; set; }                  // Maps to gender
    public long CareerId { get; set; }
    public int? AcademicPeriodId { get; set; }           // Maps to academic_period_id
    public decimal Gpa { get; set; }

    public User? User { get; set; }
}
