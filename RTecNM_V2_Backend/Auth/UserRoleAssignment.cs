using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class UserRoleAssignment : BaseEntity
{
    public long UserId { get; set; }
    public long RoleId { get; set; }

    public User? User { get; set; }
    public Role? Role { get; set; }
}
