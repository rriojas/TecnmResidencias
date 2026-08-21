using TecNM.Residency.Advisors;
using TecNM.Residency.Common;
using TecNM.Residency.Students;

namespace TecNM.Residency.Auth;

public interface IRoleRepository
{
    Task<PaginatedResult<Role>> GetPagedRolesAsync(PaginationQuery query, bool includeInactive = false);
    Task<Role?> GetRoleByIdAsync(long id);
    Task<Role?> GetRoleByCodeAsync(string code);
    Task<Role> CreateRoleAsync(Role role, List<long> permissionIds);
    Task<Role> UpdateRoleAsync(Role role, List<long> permissionIds);
    Task<bool> SoftDeleteRoleAsync(long id, long deletedByUserId);
    Task<List<Module>> GetModulesWithPermissionsAsync();
    Task<PaginatedResult<User>> GetUsersForManagementPagedAsync(PaginationQuery query, string? roleFilter, string? search, bool includeInactive = false);
    Task<List<User>> GetUsersForManagementListAsync(string? roleFilter, string? search, bool includeInactive = false);
    Task<bool> AssignUserRolesAsync(long userId, List<long> roleIds, long performedByUserId);
    Task<bool> ToggleUserAdminStatusAsync(long userId, bool isAdmin, long updatedByUserId);
    Task<User?> GetUserByIdAsync(long userId);
    Task<User> CreateUserAsync(User user, List<long> roleIds, long performedByUserId);
    Task<User> UpdateUserAsync(User user, List<long> roleIds, long performedByUserId);
    Task<Student?> GetStudentByUserIdAsync(long userId);
    Task<Advisor?> GetAdvisorByUserIdAsync(long userId);
    Task<bool> IsEmailInUseAsync(string email, long? excludeUserId = null);
    Task<bool> IsControlNumberInUseAsync(string controlNumber, long? excludeUserId = null);
    Task EnsureUserRoleAsync(long userId, string roleCode, long performedByUserId);
    Task<List<UserOptionDto>> GetUserOptionsAsync();
    Task<List<Role>> GetAllRolesForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<List<User>> GetAllUsersForExportAsync(string? search, string? roleFilter, string? sortBy, string? sortDir, bool includeInactive = false);
    Task EnsureStudentProfileAsync(long userId, string email, string? controlNum, string? firstName, string? lastName, string? lastName2, string? curp, long? careerId, long? createdByUserId, long? updatedByUserId);
    Task EnsureAdvisorProfileAsync(long userId, string email, string? fullName, string? title, long? departmentId, string? phone, int? advisorType, long? createdByUserId, long? updatedByUserId);
}
