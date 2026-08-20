using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public interface IRoleService
{
    Task<Result<PaginatedResult<RoleResponseDto>>> GetPagedRolesAsync(PaginationQuery query, bool includeInactive = false);
    Task<Result<RoleResponseDto>> GetRoleByIdAsync(long id);
    Task<Result<RoleResponseDto>> CreateRoleAsync(CreateRoleDto dto);
    Task<Result<RoleResponseDto>> UpdateRoleAsync(long id, UpdateRoleDto dto);
    Task<Result<bool>> SoftDeleteRoleAsync(long id);
    Task<Result<List<ModulePermissionsDto>>> GetModulesWithPermissionsAsync();
    Task<Result<PaginatedResult<UserRoleManagementDto>>> GetUsersForManagementPagedAsync(PaginationQuery query, string? roleFilter, string? search, bool includeInactive = false);
    Task<Result<List<UserOptionDto>>> GetUserOptionsAsync();
    Task<Result<byte[]>> ExportRolesPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<Result<byte[]>> ExportUsersPdfAsync(string? search, string? roleFilter, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<Result<bool>> AssignUserRolesAsync(long userId, AssignUserRolesDto dto);
    Task<Result<bool>> ToggleUserAdminStatusAsync(long userId, ToggleAdminDto dto);
    Task<Result<UserRoleManagementDto>> CreateUserAsync(CreateUserManagementDto dto);
    Task<Result<UserRoleManagementDto>> UpdateUserAsync(long userId, UpdateUserManagementDto dto);
}
