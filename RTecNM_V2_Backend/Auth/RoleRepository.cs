using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Advisors;
using TecNM.Residency.Common;
using TecNM.Residency.Students;

namespace TecNM.Residency.Auth;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _context;

    public RoleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Role>> GetPagedRolesAsync(PaginationQuery query, bool includeInactive = false)
    {
        IQueryable<Role> q = _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                    .ThenInclude(p => p!.Module);

        if (!includeInactive)
            q = q.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(r => r.Name.ToLower().Contains(term) || r.Code.ToLower().Contains(term));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Name", "Code", "CreatedAt" },
            "Name");

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<Role?> GetRoleByIdAsync(long id)
    {
        return await _context.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                    .ThenInclude(p => p!.Module)
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
    }

    public async Task<Role?> GetRoleByCodeAsync(string code)
    {
        return await _context.Roles
            .FirstOrDefaultAsync(r => r.Code.ToLower() == code.ToLower() && r.IsActive);
    }

    public async Task<Role> CreateRoleAsync(Role role, List<long> permissionIds)
    {
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        if (permissionIds.Count > 0)
        {
            var validPermissionIds = await _context.Permissions
                .Where(p => permissionIds.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var permId in validPermissionIds)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        return await GetRoleByIdAsync(role.Id) ?? role;
    }

    public async Task<Role> UpdateRoleAsync(Role role, List<long> permissionIds)
    {
        _context.Roles.Update(role);

        var existingRolePermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync();

        _context.RolePermissions.RemoveRange(existingRolePermissions);
        await _context.SaveChangesAsync();

        if (permissionIds.Count > 0)
        {
            var validPermissionIds = await _context.Permissions
                .Where(p => permissionIds.Contains(p.Id) && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var permId in validPermissionIds)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
        }

        return await GetRoleByIdAsync(role.Id) ?? role;
    }

    public async Task<bool> SoftDeleteRoleAsync(long id, long deletedByUserId)
    {
        var role = await _context.Roles.FindAsync(id);
        if (role == null) return false;

        role.IsActive = false;
        role.DeletedAt = DateTime.UtcNow;
        role.DeletedBy = deletedByUserId;
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = deletedByUserId;
        _context.Roles.Update(role);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Module>> GetModulesWithPermissionsAsync()
    {
        return await _context.Modules
            .Include(m => m.Permissions)
            .Where(m => m.IsActive)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    public async Task<PaginatedResult<User>> GetUsersForManagementPagedAsync(PaginationQuery query, string? roleFilter, string? search, bool includeInactive = false)
    {
        IQueryable<User> q = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role);

        if (!includeInactive)
            q = q.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(u => u.Email.ToLower().Contains(term)
                || _context.Students.Any(s => s.UserId == u.Id && ((s.FirstName + " " + s.LastName + " " + (s.LastName2 ?? "")).ToLower().Contains(term) || s.ControlNumber.ToLower().Contains(term)))
                || _context.Advisors.Any(a => a.UserId == u.Id && (a.FullName.ToLower().Contains(term) || (a.Phone != null && a.Phone.Contains(term)))));
        }

        if (roleFilter == "with_role")
            q = q.Where(u => u.UserRoles.Any(ur => ur.IsActive));
        else if (roleFilter == "without_role")
            q = q.Where(u => !u.UserRoles.Any(ur => ur.IsActive));

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "Email", "CreatedAt" },
            "Email");

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<User>> GetUsersForManagementListAsync(string? roleFilter, string? search, bool includeInactive = false)
    {
        IQueryable<User> q = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role);

        if (!includeInactive)
            q = q.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(u => u.Email.ToLower().Contains(term)
                || _context.Students.Any(s => s.UserId == u.Id && ((s.FirstName + " " + s.LastName + " " + (s.LastName2 ?? "")).ToLower().Contains(term) || s.ControlNumber.ToLower().Contains(term)))
                || _context.Advisors.Any(a => a.UserId == u.Id && (a.FullName.ToLower().Contains(term) || (a.Phone != null && a.Phone.Contains(term)))));
        }

        if (roleFilter == "with_role")
            q = q.Where(u => u.UserRoles.Any(ur => ur.IsActive));
        else if (roleFilter == "without_role")
            q = q.Where(u => !u.UserRoles.Any(ur => ur.IsActive));

        return await q.ToListAsync();
    }

    public async Task<bool> AssignUserRolesAsync(long userId, List<long> roleIds, long performedByUserId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return false;

        var existingAssignments = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync();

        _context.UserRoles.RemoveRange(existingAssignments);
        await _context.SaveChangesAsync();

        if (roleIds.Count > 0)
        {
            var validRoleIds = await _context.Roles
                .Where(r => roleIds.Contains(r.Id) && r.IsActive)
                .Select(r => r.Id)
                .ToListAsync();

            foreach (var roleId in validRoleIds)
            {
                _context.UserRoles.Add(new UserRoleAssignment
                {
                    UserId = userId,
                    RoleId = roleId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    CreatedBy = performedByUserId
                });
            }

            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<bool> ToggleUserAdminStatusAsync(long userId, bool isAdmin, long updatedByUserId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null || !user.IsActive) return false;

        user.IsAdmin = isAdmin;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = updatedByUserId;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<User?> GetUserByIdAsync(long userId)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    public async Task<User> CreateUserAsync(User user, List<long> roleIds, long performedByUserId)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        if (roleIds.Count > 0)
        {
            await AssignUserRolesAsync(user.Id, roleIds, performedByUserId);
        }

        return await GetUserByIdAsync(user.Id) ?? user;
    }

    public async Task<User> UpdateUserAsync(User user, List<long> roleIds, long performedByUserId)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        if (roleIds != null)
        {
            await AssignUserRolesAsync(user.Id, roleIds, performedByUserId);
        }

        return await GetUserByIdAsync(user.Id) ?? user;
    }

    public async Task<Student?> GetStudentByUserIdAsync(long userId)
    {
        return await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    }

    public async Task<Advisor?> GetAdvisorByUserIdAsync(long userId)
    {
        return await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == userId && a.IsActive);
    }

    public async Task<bool> IsEmailInUseAsync(string email, long? excludeUserId = null)
    {
        var cleanEmail = (email ?? "").Trim().ToLowerInvariant();
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == cleanEmail && (!excludeUserId.HasValue || u.Id != excludeUserId.Value));
    }

    public async Task<bool> IsControlNumberInUseAsync(string controlNumber, long? excludeUserId = null)
    {
        var cleanControlNum = (controlNumber ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(cleanControlNum)) return false;
        return await _context.Students.AnyAsync(s => s.ControlNumber.ToUpper() == cleanControlNum && (!excludeUserId.HasValue || s.UserId != excludeUserId.Value));
    }

    public async Task EnsureUserRoleAsync(long userId, string roleCode, long performedByUserId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user == null) return;

        var role = await _context.Roles
            .FirstOrDefaultAsync(r => r.Code.ToLower() == roleCode.ToLower() && r.IsActive);
        if (role == null) return;

        var existing = await _context.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == role.Id);
        if (existing != null) return;

        _context.UserRoles.Add(new UserRoleAssignment
        {
            UserId = userId,
            RoleId = role.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = performedByUserId
        });
        await _context.SaveChangesAsync();
    }

    public async Task<List<UserOptionDto>> GetUserOptionsAsync()
    {
        return await _context.Users
            .Where(u => u.IsActive && !_context.Advisors.Any(a => a.UserId == u.Id))
            .OrderBy(u => u.Email)
            .Select(u => new UserOptionDto { Id = u.Id, Email = u.Email })
            .ToListAsync();
    }

    public async Task<List<Role>> GetAllRolesForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<Role> q = _context.Roles.AsNoTracking();

        if (!includeInactive)
            q = q.Where(r => r.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(r => r.Name.ToLower().Contains(term) || r.Code.ToLower().Contains(term));
        }

        q = q.ApplySort(sortBy, sortDir,
            new[] { "Name", "Code", "CreatedAt" },
            "Name");

        return await q.Take(1000).ToListAsync();
    }

    public async Task<List<User>> GetAllUsersForExportAsync(string? search, string? roleFilter, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<User> q = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking();

        if (!includeInactive)
            q = q.Where(u => u.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(u => u.Email.ToLower().Contains(term));
        }

        if (roleFilter == "with_role")
            q = q.Where(u => u.UserRoles.Any(ur => ur.IsActive));
        else if (roleFilter == "without_role")
            q = q.Where(u => !u.UserRoles.Any(ur => ur.IsActive));

        q = q.ApplySort(sortBy, sortDir,
            new[] { "Email", "CreatedAt" },
            "Email");

        return await q.Take(1000).ToListAsync();
    }

    public async Task EnsureStudentProfileAsync(long userId, string email, string? controlNum, string? firstName, string? lastName, string? lastName2, string? curp, string? gender, long? careerId, int? academicPeriodId, long? createdByUserId, long? updatedByUserId)
    {
        var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
        var namePart = (email ?? "").Split('@')[0];
        var finalControlNum = !string.IsNullOrWhiteSpace(controlNum) ? controlNum.Trim().ToUpperInvariant() : ("26" + userId.ToString("D6"));

        if (existingStudent == null)
        {
            _context.Students.Add(new Student
            {
                UserId = userId,
                ControlNumber = finalControlNum,
                FirstName = !string.IsNullOrWhiteSpace(firstName) ? firstName.Trim() : namePart,
                LastName = !string.IsNullOrWhiteSpace(lastName) ? lastName.Trim() : "Alumno",
                LastName2 = lastName2?.Trim(),
                Curp = curp?.Trim().ToUpperInvariant(),
                Gender = gender?.Trim(),
                CareerId = careerId > 0 ? careerId.Value : 1,
                AcademicPeriodId = academicPeriodId,
                IsActive = true,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId
            });
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(controlNum)) existingStudent.ControlNumber = controlNum.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(firstName)) existingStudent.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName)) existingStudent.LastName = lastName.Trim();
            if (lastName2 != null) existingStudent.LastName2 = lastName2.Trim();
            if (curp != null) existingStudent.Curp = curp.Trim().ToUpperInvariant();
            if (gender != null) existingStudent.Gender = gender.Trim();
            if (academicPeriodId.HasValue) existingStudent.AcademicPeriodId = academicPeriodId.Value;
            if (careerId > 0) existingStudent.CareerId = careerId.Value;
            existingStudent.UpdatedAt = DateTime.UtcNow;
            existingStudent.UpdatedBy = updatedByUserId;
            _context.Students.Update(existingStudent);
        }
        await _context.SaveChangesAsync();
    }

    public async Task EnsureAdvisorProfileAsync(long userId, string email, string? fullName, string? title, long? departmentId, string? phone, int? advisorType, long? createdByUserId, long? updatedByUserId)
    {
        var existingAdvisor = await _context.Advisors.FirstOrDefaultAsync(a => a.UserId == userId);
        var namePart = (email ?? "").Split('@')[0];

        if (existingAdvisor == null)
        {
            _context.Advisors.Add(new Advisor
            {
                UserId = userId,
                FullName = !string.IsNullOrWhiteSpace(fullName) ? fullName.Trim() : namePart,
                Title = !string.IsNullOrWhiteSpace(title) ? title.Trim() : "Asesor Académico",
                DepartmentId = departmentId > 0 ? departmentId.Value : 1,
                Phone = phone?.Trim(),
                AdvisorType = advisorType.HasValue && advisorType.Value == 1 ? AdvisorType.Internal : AdvisorType.External,
                IsActive = true,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = createdByUserId
            });
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(fullName)) existingAdvisor.FullName = fullName.Trim();
            if (!string.IsNullOrWhiteSpace(title)) existingAdvisor.Title = title.Trim();
            if (departmentId > 0) existingAdvisor.DepartmentId = departmentId.Value;
            if (phone != null) existingAdvisor.Phone = phone.Trim();
            if (advisorType.HasValue) existingAdvisor.AdvisorType = advisorType.Value == 1 ? AdvisorType.Internal : AdvisorType.External;
            existingAdvisor.UpdatedAt = DateTime.UtcNow;
            existingAdvisor.UpdatedBy = updatedByUserId;
            _context.Advisors.Update(existingAdvisor);
        }
        await _context.SaveChangesAsync();
    }
}
