using System.ComponentModel.DataAnnotations.Schema;
using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

[Table("advisor_departments")]
public class AdvisorDepartment : BaseEntity
{
    public long AdvisorId { get; set; }
    public long DepartmentId { get; set; }

    public Advisor? Advisor { get; set; }
}
