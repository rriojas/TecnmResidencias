using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class CareerHead : BaseEntity
{
    public long UserId { get; set; }
    public long CareerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Phone { get; set; }

    public User? User { get; set; }
}
