using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class Permission : BaseEntity
{
    public long ModuleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public Module? Module { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
