using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class UserCareer : BaseEntity
{
    public long UserId { get; set; }
    public long CareerId { get; set; }

    public User? User { get; set; }
    public Career? Career { get; set; }
}
