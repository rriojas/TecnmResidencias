using TecNM.Residency.Common;

namespace TecNM.Residency.Auth;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUser;

    public RoleService(IRoleRepository roleRepository, ICurrentUserService currentUser)
    {
        _roleRepository = roleRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<RoleResponseDto>>> GetPagedRolesAsync(PaginationQuery query, bool includeInactive = false)
    {
        var paged = await _roleRepository.GetPagedRolesAsync(query, includeInactive);
        var dtos = paged.Items.Select(MapRoleToDto);
        var result = PaginatedResult<RoleResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<RoleResponseDto>>.Success(result);
    }

    public async Task<Result<RoleResponseDto>> GetRoleByIdAsync(long id)
    {
        var role = await _roleRepository.GetRoleByIdAsync(id);
        if (role == null)
            return Result<RoleResponseDto>.Failure("Rol no encontrado", 404);

        return Result<RoleResponseDto>.Success(MapRoleToDto(role));
    }

    public async Task<Result<RoleResponseDto>> CreateRoleAsync(CreateRoleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
            return Result<RoleResponseDto>.Failure("El código y el nombre del rol son obligatorios", 400);

        var existing = await _roleRepository.GetRoleByCodeAsync(dto.Code);
        if (existing != null)
            return Result<RoleResponseDto>.Failure("Ya existe un rol con ese código", 400);

        var role = new Role
        {
            Code = dto.Code.Trim().ToLowerInvariant(),
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            IsActive = true,
            IsVisible = true,
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var created = await _roleRepository.CreateRoleAsync(role, dto.PermissionIds ?? new List<long>());
        return Result<RoleResponseDto>.Success(MapRoleToDto(created));
    }

    public async Task<Result<RoleResponseDto>> UpdateRoleAsync(long id, UpdateRoleDto dto)
    {
        var role = await _roleRepository.GetRoleByIdAsync(id);
        if (role == null)
            return Result<RoleResponseDto>.Failure("Rol no encontrado", 404);

        if (string.IsNullOrWhiteSpace(dto.Name))
            return Result<RoleResponseDto>.Failure("El nombre del rol es obligatorio", 400);

        role.Name = dto.Name.Trim();
        role.Description = dto.Description?.Trim();
        role.UpdatedAt = DateTime.UtcNow;
        role.UpdatedBy = _currentUser.UserId;

        var updated = await _roleRepository.UpdateRoleAsync(role, dto.PermissionIds ?? new List<long>());
        return Result<RoleResponseDto>.Success(MapRoleToDto(updated));
    }

    public async Task<Result<bool>> SoftDeleteRoleAsync(long id)
    {
        var role = await _roleRepository.GetRoleByIdAsync(id);
        if (role == null)
            return Result<bool>.Failure("Rol no encontrado", 404);

        var success = await _roleRepository.SoftDeleteRoleAsync(id, _currentUser.UserId);
        return Result<bool>.Success(success);
    }

    public async Task<Result<List<ModulePermissionsDto>>> GetModulesWithPermissionsAsync()
    {
        var modules = await _roleRepository.GetModulesWithPermissionsAsync();
        var dtos = modules.Select(m => new ModulePermissionsDto
        {
            ModuleId = m.Id,
            ModuleName = m.Name,
            ModuleSlug = m.Slug,
            Permissions = m.Permissions.Where(p => p.IsActive).Select(p => new PermissionItemDto
            {
                Id = p.Id,
                ModuleId = p.ModuleId,
                ModuleName = m.Name,
                Name = p.Name,
                Slug = p.Slug
            }).ToList()
        }).ToList();

        return Result<List<ModulePermissionsDto>>.Success(dtos);
    }

    public async Task<Result<PaginatedResult<UserRoleManagementDto>>> GetUsersForManagementPagedAsync(PaginationQuery query, string? roleFilter, string? search, bool includeInactive = false)
    {
        var usersList = await _roleRepository.GetUsersForManagementListAsync(roleFilter, search, includeInactive);
        var dtos = new List<UserRoleManagementDto>();

        foreach (var u in usersList)
        {
            var dto = MapUserToDto(u);

            var student = await _roleRepository.GetStudentByUserIdAsync(u.Id);
            if (student != null)
            {
                dto.ControlNumber = student.ControlNumber;
                dto.FirstName = student.FirstName;
                dto.LastName = student.LastName;
                dto.LastName2 = student.LastName2;
                dto.Curp = student.Curp;
                dto.CareerId = student.CareerId;
            }

            var advisor = await _roleRepository.GetAdvisorByUserIdAsync(u.Id);
            if (advisor != null)
            {
                dto.FullName = advisor.FullName;
                dto.Title = advisor.Title;
                dto.DepartmentId = advisor.DepartmentId;
                if (!string.IsNullOrWhiteSpace(advisor.Phone))
                {
                    dto.Phone = advisor.Phone;
                }
                dto.AdvisorType = (int)advisor.AdvisorType;

                if (string.IsNullOrWhiteSpace(dto.FirstName) && !string.IsNullOrWhiteSpace(advisor.FullName))
                {
                    var parts = advisor.FullName.Trim().Split(' ', 2);
                    dto.FirstName = parts[0];
                    if (parts.Length > 1) dto.LastName = parts[1];
                }
            }

            dtos.Add(dto);
        }

        var isDesc = (query.SortDir ?? "").Equals("desc", StringComparison.OrdinalIgnoreCase);
        var sortBy = (query.SortBy ?? "Email").Trim();

        dtos = (sortBy.ToLowerInvariant() switch
        {
            "controlnumber" or "matricula" => isDesc
                ? dtos.OrderByDescending(d => d.ControlNumber ?? "").ThenBy(d => d.Email).ToList()
                : dtos.OrderBy(d => d.ControlNumber ?? "").ThenBy(d => d.Email).ToList(),

            "phone" or "telefono" => isDesc
                ? dtos.OrderByDescending(d => d.Phone ?? "").ThenBy(d => d.Email).ToList()
                : dtos.OrderBy(d => d.Phone ?? "").ThenBy(d => d.Email).ToList(),

            "firstname" or "name" or "nombre" => isDesc
                ? dtos.OrderByDescending(d => $"{d.FirstName} {d.LastName}".Trim()).ThenBy(d => d.Email).ToList()
                : dtos.OrderBy(d => $"{d.FirstName} {d.LastName}".Trim()).ThenBy(d => d.Email).ToList(),

            "role" or "rol" => isDesc
                ? dtos.OrderByDescending(d => d.AssignedRoles.FirstOrDefault()?.Name ?? (d.IsAdmin ? "SuperAdministrador" : "Z")).ThenBy(d => d.Email).ToList()
                : dtos.OrderBy(d => d.AssignedRoles.FirstOrDefault()?.Name ?? (d.IsAdmin ? "SuperAdministrador" : "Z")).ThenBy(d => d.Email).ToList(),

            "createdat" => isDesc
                ? dtos.OrderByDescending(d => d.CreatedAt).ToList()
                : dtos.OrderBy(d => d.CreatedAt).ToList(),

            _ => isDesc
                ? dtos.OrderByDescending(d => d.Email).ToList()
                : dtos.OrderBy(d => d.Email).ToList()
        });

        var totalCount = dtos.Count;
        var pagedItems = dtos
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var result = PaginatedResult<UserRoleManagementDto>.Create(
            pagedItems, totalCount, query.PageNumber, query.PageSize);
        return Result<PaginatedResult<UserRoleManagementDto>>.Success(result);
    }

    public async Task<Result<List<UserOptionDto>>> GetUserOptionsAsync()
    {
        var users = await _roleRepository.GetUserOptionsAsync();
        return Result<List<UserOptionDto>>.Success(users);
    }

    public async Task<Result<byte[]>> ExportRolesPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        var roles = await _roleRepository.GetAllRolesForExportAsync(search, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Catálogo de Roles y Permisos - TecNM Campus Monclova",
            Headers = new List<string> { "Código", "Nombre", "Descripción" },
            Rows = roles.Select(r => new List<string>
            {
                r.Code,
                r.Name,
                r.Description ?? string.Empty
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    public async Task<Result<byte[]>> ExportUsersPdfAsync(string? search, string? roleFilter, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        var users = await _roleRepository.GetAllUsersForExportAsync(search, roleFilter, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Usuarios del Sistema - TecNM Campus Monclova",
            Headers = new List<string> { "ID", "Correo", "Rol(es) Asignado(s)", "SuperAdministrador" },
            Rows = users.Select(u => new List<string>
            {
                u.Id.ToString(),
                u.Email,
                string.Join(", ", u.UserRoles.Where(ur => ur.IsActive && ur.Role != null).Select(ur => ur.Role!.Name)),
                u.IsAdmin ? "Sí" : "No"
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    public async Task<Result<bool>> AssignUserRolesAsync(long userId, AssignUserRolesDto dto)
    {
        var success = await _roleRepository.AssignUserRolesAsync(userId, dto.RoleIds ?? new List<long>(), _currentUser.UserId);
        if (!success)
            return Result<bool>.Failure("Usuario no encontrado o inactivo", 404);

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ToggleUserAdminStatusAsync(long userId, ToggleAdminDto dto)
    {
        var success = await _roleRepository.ToggleUserAdminStatusAsync(userId, dto.IsAdmin, _currentUser.UserId);
        if (!success)
            return Result<bool>.Failure("Usuario no encontrado o inactivo", 404);

        return Result<bool>.Success(true);
    }

    public async Task<Result<UserRoleManagementDto>> CreateUserAsync(CreateUserManagementDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return Result<UserRoleManagementDto>.Failure("El correo y la contraseña son obligatorios", 400);

        if (!InstitutionalEmail.IsValid(dto.Email))
            return Result<UserRoleManagementDto>.Failure(InstitutionalEmail.ErrorMessage, 400);

        if (dto.RoleId <= 0)
            return Result<UserRoleManagementDto>.Failure("Debe seleccionar un rol para el usuario", 400);

        var emailUsed = await _roleRepository.IsEmailInUseAsync(dto.Email);
        if (emailUsed)
            return Result<UserRoleManagementDto>.Failure("El correo electrónico ya se encuentra registrado.", 400);

        var selectedRole = await _roleRepository.GetRoleByIdAsync(dto.RoleId);
        if (selectedRole == null)
            return Result<UserRoleManagementDto>.Failure("El rol seleccionado no existe", 404);

        var isSuperAdmin = selectedRole.Code.Equals("superadmin", StringComparison.OrdinalIgnoreCase) || selectedRole.Code.Equals("admin", StringComparison.OrdinalIgnoreCase);
        var baseUserRole = MapCodeToUserRole(selectedRole.Code);

        if (baseUserRole == UserRole.Student && !string.IsNullOrWhiteSpace(dto.ControlNumber))
        {
            var controlNumUsed = await _roleRepository.IsControlNumberInUseAsync(dto.ControlNumber);
            if (controlNumUsed)
                return Result<UserRoleManagementDto>.Failure("El número de control ya se encuentra registrado.", 400);
        }

        try
        {
            var user = new User
            {
                Email = dto.Email.Trim().ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = baseUserRole,
                IsAdmin = isSuperAdmin,
                IsActive = true,
                IsVisible = true,
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            };

            var created = await _roleRepository.CreateUserAsync(user, new List<long> { dto.RoleId }, _currentUser.UserId);

            var advisorFullName = dto.FullName;
            if (string.IsNullOrWhiteSpace(advisorFullName) && (!string.IsNullOrWhiteSpace(dto.FirstName) || !string.IsNullOrWhiteSpace(dto.LastName)))
            {
                advisorFullName = $"{dto.FirstName} {dto.LastName}".Trim();
            }

            if (baseUserRole == UserRole.Student || !string.IsNullOrWhiteSpace(dto.ControlNumber) || !string.IsNullOrWhiteSpace(dto.FirstName))
            {
                await _roleRepository.EnsureStudentProfileAsync(created.Id, created.Email, dto.ControlNumber, dto.FirstName, dto.LastName, dto.LastName2, dto.Curp, dto.CareerId, _currentUser.UserId, _currentUser.UserId);
            }

            if (baseUserRole == UserRole.Advisor || !string.IsNullOrWhiteSpace(dto.Phone) || !string.IsNullOrWhiteSpace(advisorFullName) || !string.IsNullOrWhiteSpace(dto.Title))
            {
                await _roleRepository.EnsureAdvisorProfileAsync(created.Id, created.Email, advisorFullName, dto.Title, dto.DepartmentId, dto.Phone, dto.AdvisorType, _currentUser.UserId, _currentUser.UserId);
            }

            var responseDto = MapUserToDto(created);
            var createdStudent = await _roleRepository.GetStudentByUserIdAsync(created.Id);
            if (createdStudent != null)
            {
                responseDto.ControlNumber = createdStudent.ControlNumber;
                responseDto.FirstName = createdStudent.FirstName;
                responseDto.LastName = createdStudent.LastName;
                responseDto.LastName2 = createdStudent.LastName2;
                responseDto.Curp = createdStudent.Curp;
                responseDto.CareerId = createdStudent.CareerId;
            }
            var createdAdvisor = await _roleRepository.GetAdvisorByUserIdAsync(created.Id);
            if (createdAdvisor != null)
            {
                responseDto.FullName = createdAdvisor.FullName;
                responseDto.Title = createdAdvisor.Title;
                responseDto.DepartmentId = createdAdvisor.DepartmentId;
                responseDto.Phone = createdAdvisor.Phone;
                responseDto.AdvisorType = (int)createdAdvisor.AdvisorType;

                if (string.IsNullOrWhiteSpace(responseDto.FirstName) && !string.IsNullOrWhiteSpace(createdAdvisor.FullName))
                {
                    var parts = createdAdvisor.FullName.Trim().Split(' ', 2);
                    responseDto.FirstName = parts[0];
                    if (parts.Length > 1) responseDto.LastName = parts[1];
                }
            }

            return Result<UserRoleManagementDto>.Success(responseDto);
        }
        catch (Exception ex)
        {
            return Result<UserRoleManagementDto>.Failure(ex.InnerException?.Message ?? ex.Message, 500);
        }
    }

    public async Task<Result<UserRoleManagementDto>> UpdateUserAsync(long userId, UpdateUserManagementDto dto)
    {
        var user = await _roleRepository.GetUserByIdAsync(userId);
        if (user == null)
            return Result<UserRoleManagementDto>.Failure("Usuario no encontrado", 404);

        if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Trim().Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            if (!InstitutionalEmail.IsValid(dto.Email))
                return Result<UserRoleManagementDto>.Failure(InstitutionalEmail.ErrorMessage, 400);

            var emailUsed = await _roleRepository.IsEmailInUseAsync(dto.Email, userId);
            if (emailUsed)
                return Result<UserRoleManagementDto>.Failure("El correo electrónico ya se encuentra registrado por otro usuario.", 400);
            user.Email = dto.Email.Trim().ToLowerInvariant();
        }

        if (user.Role == UserRole.Student && !string.IsNullOrWhiteSpace(dto.ControlNumber))
        {
            var controlNumUsed = await _roleRepository.IsControlNumberInUseAsync(dto.ControlNumber, userId);
            if (controlNumUsed)
                return Result<UserRoleManagementDto>.Failure("El número de control ya se encuentra registrado por otro estudiante.", 400);
        }

        if (dto.RoleId > 0)
        {
            var selectedRole = await _roleRepository.GetRoleByIdAsync(dto.RoleId);
            if (selectedRole != null)
            {
                var isSuperAdmin = selectedRole.Code.Equals("superadmin", StringComparison.OrdinalIgnoreCase) || selectedRole.Code.Equals("admin", StringComparison.OrdinalIgnoreCase);
                user.IsAdmin = isSuperAdmin;
                user.Role = MapCodeToUserRole(selectedRole.Code);
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = _currentUser.UserId;

        try
        {
            var roleIdsList = dto.RoleId > 0 ? new List<long> { dto.RoleId } : null;
            var updated = await _roleRepository.UpdateUserAsync(user, roleIdsList!, _currentUser.UserId);

            var advisorFullName = dto.FullName;
            if (string.IsNullOrWhiteSpace(advisorFullName) && (!string.IsNullOrWhiteSpace(dto.FirstName) || !string.IsNullOrWhiteSpace(dto.LastName)))
            {
                advisorFullName = $"{dto.FirstName} {dto.LastName}".Trim();
            }

            if (updated.Role == UserRole.Student || !string.IsNullOrWhiteSpace(dto.ControlNumber) || !string.IsNullOrWhiteSpace(dto.FirstName))
            {
                await _roleRepository.EnsureStudentProfileAsync(updated.Id, updated.Email, dto.ControlNumber, dto.FirstName, dto.LastName, dto.LastName2, dto.Curp, dto.CareerId, _currentUser.UserId, _currentUser.UserId);
            }

            if (updated.Role == UserRole.Advisor || !string.IsNullOrWhiteSpace(dto.Phone) || !string.IsNullOrWhiteSpace(advisorFullName) || !string.IsNullOrWhiteSpace(dto.Title))
            {
                await _roleRepository.EnsureAdvisorProfileAsync(updated.Id, updated.Email, advisorFullName, dto.Title, dto.DepartmentId, dto.Phone, dto.AdvisorType, _currentUser.UserId, _currentUser.UserId);
            }

            var responseDto = MapUserToDto(updated);
            var updatedStudent = await _roleRepository.GetStudentByUserIdAsync(updated.Id);
            if (updatedStudent != null)
            {
                responseDto.ControlNumber = updatedStudent.ControlNumber;
                responseDto.FirstName = updatedStudent.FirstName;
                responseDto.LastName = updatedStudent.LastName;
                responseDto.LastName2 = updatedStudent.LastName2;
                responseDto.Curp = updatedStudent.Curp;
                responseDto.CareerId = updatedStudent.CareerId;
            }
            var updatedAdvisor = await _roleRepository.GetAdvisorByUserIdAsync(updated.Id);
            if (updatedAdvisor != null)
            {
                responseDto.FullName = updatedAdvisor.FullName;
                responseDto.Title = updatedAdvisor.Title;
                responseDto.DepartmentId = updatedAdvisor.DepartmentId;
                responseDto.Phone = updatedAdvisor.Phone;
                responseDto.AdvisorType = (int)updatedAdvisor.AdvisorType;

                if (string.IsNullOrWhiteSpace(responseDto.FirstName) && !string.IsNullOrWhiteSpace(updatedAdvisor.FullName))
                {
                    var parts = updatedAdvisor.FullName.Trim().Split(' ', 2);
                    responseDto.FirstName = parts[0];
                    if (parts.Length > 1) responseDto.LastName = parts[1];
                }
            }

            return Result<UserRoleManagementDto>.Success(responseDto);
        }
        catch (Exception ex)
        {
            return Result<UserRoleManagementDto>.Failure(ex.InnerException?.Message ?? ex.Message, 500);
        }
    }

    private static UserRole MapCodeToUserRole(string code) => (code ?? "").ToLowerInvariant().Replace("_", "") switch
    {
        "estudiante" or "student" => UserRole.Student,
        "asesor" or "advisor" => UserRole.Advisor,
        "academico" or "academic" or "jefatura" or "departmenthead" => UserRole.Academic,
        "vinculacion" => UserRole.Vinculacion,
        "director" => UserRole.Director,
        "superadmin" or "admin" => UserRole.Admin,
        _ => UserRole.Student
    };

    private static UserRoleManagementDto MapUserToDto(User u) => new UserRoleManagementDto
    {
        UserId = u.Id,
        Email = u.Email,
        Role = u.Role.ToString().ToLowerInvariant(),
        IsAdmin = u.IsAdmin,
        IsActive = u.IsActive,
        IsVisible = u.IsVisible,
        DisplayOrder = u.DisplayOrder,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
        CreatedBy = u.CreatedBy,
        UpdatedBy = u.UpdatedBy,
        DeletedBy = u.DeletedBy,
        DeletedAt = u.DeletedAt,
        AssignedRoles = u.UserRoles
            .Where(ur => ur.IsActive && ur.Role != null && ur.Role.IsActive)
            .Select(ur => new RoleSummaryDto
            {
                Id = ur.Role!.Id,
                Code = ur.Role.Code,
                Name = ur.Role.Name
            }).ToList()
    };

    private static RoleResponseDto MapRoleToDto(Role role)
    {
        return new RoleResponseDto
        {
            Id = role.Id,
            Code = role.Code,
            Name = role.Name,
            Description = role.Description,
            IsActive = role.IsActive,
            IsVisible = role.IsVisible,
            DisplayOrder = role.DisplayOrder,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt,
            CreatedBy = role.CreatedBy,
            UpdatedBy = role.UpdatedBy,
            DeletedBy = role.DeletedBy,
            DeletedAt = role.DeletedAt,
            Permissions = role.RolePermissions
                .Where(rp => rp.IsActive && rp.Permission != null && rp.Permission.IsActive)
                .Select(rp => new PermissionItemDto
                {
                    Id = rp.Permission!.Id,
                    ModuleId = rp.Permission.ModuleId,
                    ModuleName = rp.Permission.Module?.Name ?? string.Empty,
                    Name = rp.Permission.Name,
                    Slug = rp.Permission.Slug
                }).ToList()
        };
    }
}
