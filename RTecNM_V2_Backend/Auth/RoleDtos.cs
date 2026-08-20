namespace TecNM.Residency.Auth;

public class CreateRoleDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<long> PermissionIds { get; set; } = new();
}

public class UpdateRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<long> PermissionIds { get; set; } = new();
}

public class RoleResponseDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public long? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<PermissionItemDto> Permissions { get; set; } = new();
}

public class PermissionItemDto
{
    public long Id { get; set; }
    public long ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class ModulePermissionsDto
{
    public long ModuleId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string ModuleSlug { get; set; } = string.Empty;
    public List<PermissionItemDto> Permissions { get; set; } = new();
}

public class UserRoleManagementDto
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public long? UpdatedBy { get; set; }
    public long? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<RoleSummaryDto> AssignedRoles { get; set; } = new();

    // Linked Student profile data
    public string? ControlNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LastName2 { get; set; }
    public string? Curp { get; set; }
    public long? CareerId { get; set; }

    // Linked Advisor profile data
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public long? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public int? AdvisorType { get; set; }
}

public class RoleSummaryDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UserOptionDto
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
}

public class AssignUserRolesDto
{
    public List<long> RoleIds { get; set; } = new();
}

public class ToggleAdminDto
{
    public bool IsAdmin { get; set; }
}

public class CreateUserManagementDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public long RoleId { get; set; }

    // Student fields
    public string? ControlNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LastName2 { get; set; }
    public string? Curp { get; set; }
    public long? CareerId { get; set; }

    // Advisor fields
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public long? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public int? AdvisorType { get; set; }
}

public class UpdateUserManagementDto
{
    public string Email { get; set; } = string.Empty;
    public string? NewPassword { get; set; }
    public long RoleId { get; set; }

    // Student fields
    public string? ControlNumber { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? LastName2 { get; set; }
    public string? Curp { get; set; }
    public long? CareerId { get; set; }

    // Advisor fields
    public string? FullName { get; set; }
    public string? Title { get; set; }
    public long? DepartmentId { get; set; }
    public string? Phone { get; set; }
    public int? AdvisorType { get; set; }
}
