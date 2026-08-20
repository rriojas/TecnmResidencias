using TecNM.Residency.Auth;
using TecNM.Residency.Common;

namespace TecNM.Residency.Advisors;

public class Advisor : BaseEntity
{
    public long UserId { get; set; }
    public long DepartmentId { get; set; }
    public AdvisorType AdvisorType { get; set; } = AdvisorType.Internal;
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Phone { get; set; }

    public User? User { get; set; }
}
