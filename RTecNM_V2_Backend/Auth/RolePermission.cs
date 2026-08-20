using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class RolePermission : BaseEntity
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}
