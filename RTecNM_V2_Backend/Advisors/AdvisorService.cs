using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;
using TecNM.Residency.Students;

namespace TecNM.Residency.Advisors;

public class AdvisorService : IAdvisorService
{
    private readonly IAdvisorRepository _advisorRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;

    public AdvisorService(
        IAdvisorRepository advisorRepository,
        IProjectRepository projectRepository,
        IStudentRepository studentRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUser,
        AppDbContext context)
    {
        _advisorRepository = advisorRepository;
        _projectRepository = projectRepository;
        _studentRepository = studentRepository;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<Result<PaginatedResult<AdvisorResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        var paged = await _advisorRepository.GetPagedAsync(query, status, includeInactive);
        var dtos = paged.Items.Select(MapToResponseDto);
        var result = PaginatedResult<AdvisorResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<AdvisorResponseDto>>.Success(result);
    }

    public async Task<Result<byte[]>> ExportPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        var advisors = await _advisorRepository.GetAllForExportAsync(search, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Asesores Institucionales - TecNM Campus Monclova",
            Headers = new List<string> { "ID", "Nombre Completo", "Tipo", "Título", "Teléfono", "Estado", "Creado el", "Actualizado el" },
            Rows = advisors.Select(a => new List<string>
            {
                a.Id.ToString(),
                a.FullName,
                a.AdvisorType == AdvisorType.Internal ? "Interno" : "Externo",
                a.Title ?? string.Empty,
                a.Phone ?? string.Empty,
                a.IsActive ? "Activo" : "Inactivo",
                a.CreatedAt.ToString("dd/MM/yyyy"),
                a.UpdatedAt.ToString("dd/MM/yyyy")
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    public async Task<Result<List<AdvisorOptionDto>>> GetOptionsAsync()
    {
        var advisors = await _advisorRepository.GetOptionsAsync();
        var dtos = advisors.Select(a => new AdvisorOptionDto
        {
            Id = a.Id,
            FullName = a.FullName
        }).ToList();

        return Result<List<AdvisorOptionDto>>.Success(dtos);
    }

    public async Task<Result<AdvisorResponseDto>> GetAdvisorByIdAsync(long id)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<AdvisorResponseDto>.Failure("Asesor no encontrado.");
        }

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(advisor));
    }

    public async Task<Result<AdvisorResponseDto>> GetMeAsync(long userId)
    {
        if (userId <= 0)
            return Result<AdvisorResponseDto>.Failure("Sesión no autenticada.", 401);

        var advisor = await _advisorRepository.GetByUserIdAsync(userId);
        if (advisor == null)
            return Result<AdvisorResponseDto>.Failure("No se encontró un perfil de asesor asociado a tu cuenta.", 404);

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(advisor));
    }

    public async Task<Result<AdvisorResponseDto>> CreateAdvisorAsync(CreateAdvisorDto dto)
    {
        var resolvedName = !string.IsNullOrWhiteSpace(dto.FullName)
            ? dto.FullName.Trim()
            : $"{dto.FirstName?.Trim()} {dto.LastName?.Trim()}".Trim();

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return Result<AdvisorResponseDto>.Failure("El nombre y apellidos del asesor son obligatorios.");
        }

        var cleanEmail = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(cleanEmail) || !cleanEmail.Contains('@') || !cleanEmail.Contains('.'))
        {
            return Result<AdvisorResponseDto>.Failure("Debe ingresar un correo electrónico válido para la cuenta del asesor.");
        }

        long userId;
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == cleanEmail);
        if (existingUser != null)
        {
            userId = existingUser.Id;
            var existingAdvisor = await _advisorRepository.GetByUserIdAsync(userId);
            if (existingAdvisor != null)
            {
                return Result<AdvisorResponseDto>.Failure($"Ya existe un asesor registrado con el correo '{cleanEmail}'.");
            }
            await _roleRepository.EnsureUserRoleAsync(userId, "advisor", _currentUser.UserId);
        }
        else
        {
            var rawPassword = string.IsNullOrWhiteSpace(dto.Password) ? "Docente2026!" : dto.Password.Trim();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword);

            var newUser = new User
            {
                Email = cleanEmail,
                PasswordHash = passwordHash,
                Role = UserRole.Advisor,
                IsActive = true,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            userId = newUser.Id;

            await _roleRepository.EnsureUserRoleAsync(userId, "advisor", _currentUser.UserId);
        }

        var departmentId = dto.DepartmentId;
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            departmentId = _currentUser.CareerId.Value;
        }

        var advisor = new Advisor
        {
            UserId = userId,
            DepartmentId = departmentId,
            AdvisorType = dto.AdvisorType,
            FullName = resolvedName,
            Title = dto.Title?.Trim(),
            Phone = dto.Phone?.Trim(),
            IsActive = true,
            IsVisible = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var created = await _advisorRepository.AddAsync(advisor);

        var deptLink = await _context.AdvisorDepartments.FirstOrDefaultAsync(ad => ad.AdvisorId == created.Id && ad.DepartmentId == departmentId);
        if (deptLink == null)
        {
            _context.AdvisorDepartments.Add(new AdvisorDepartment
            {
                AdvisorId = created.Id,
                DepartmentId = departmentId,
                IsActive = true,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            });
            await _context.SaveChangesAsync();
        }

        await _roleRepository.EnsureUserRoleAsync(created.UserId, "advisor", _currentUser.UserId);

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(created));
    }

    public async Task<Result<AdvisorResponseDto>> UpdateAdvisorAsync(long id, UpdateAdvisorDto dto)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<AdvisorResponseDto>.Failure("Asesor no encontrado.");
        }

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            advisor.DepartmentId = _currentUser.CareerId.Value;
        }
        else
        {
            advisor.DepartmentId = dto.DepartmentId;
        }
        var resolvedName = !string.IsNullOrWhiteSpace(dto.FullName)
            ? dto.FullName.Trim()
            : $"{dto.FirstName?.Trim()} {dto.LastName?.Trim()}".Trim();

        if (!string.IsNullOrWhiteSpace(resolvedName))
        {
            advisor.FullName = resolvedName;
        }
        advisor.AdvisorType = dto.AdvisorType;
        advisor.Title = dto.Title;
        advisor.Phone = dto.Phone;
        advisor.UpdatedAt = DateTime.UtcNow;
        advisor.UpdatedBy = _currentUser.UserId;

        await _advisorRepository.UpdateAsync(advisor);

        var deptLink = await _context.AdvisorDepartments.FirstOrDefaultAsync(ad => ad.AdvisorId == advisor.Id && ad.DepartmentId == advisor.DepartmentId);
        if (deptLink == null)
        {
            _context.AdvisorDepartments.Add(new AdvisorDepartment
            {
                AdvisorId = advisor.Id,
                DepartmentId = advisor.DepartmentId,
                IsActive = true,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId
            });
            await _context.SaveChangesAsync();
        }

        return Result<AdvisorResponseDto>.Success(MapToResponseDto(advisor));
    }

    public async Task<Result<bool>> SoftDeleteAdvisorAsync(long id, long deletedByUserId)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<bool>.Failure("Asesor no encontrado.");
        }

        advisor.IsActive = false;
        advisor.DeletedAt = DateTime.UtcNow;
        advisor.DeletedBy = deletedByUserId;
        await _advisorRepository.UpdateAsync(advisor);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReactivateAdvisorAsync(long id)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<bool>.Failure("Asesor no encontrado.");
        }

        advisor.IsActive = true;
        advisor.DeletedAt = null;
        advisor.DeletedBy = null;
        advisor.UpdatedAt = DateTime.UtcNow;
        await _advisorRepository.UpdateAsync(advisor);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> AssignAdvisorAsync(AssignAdvisorDto dto)
    {
        var advisor = await _advisorRepository.GetByIdAsync(dto.AdvisorId);
        if (advisor == null)
        {
            return Result<bool>.Failure("Asesor no encontrado.");
        }

        var project = await _projectRepository.GetByIdAsync(dto.ProjectId);
        if (project == null)
        {
            return Result<bool>.Failure("Anteproyecto no encontrado.", 404);
        }

        project.AdvisorId = dto.AdvisorId;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;
        await _projectRepository.UpdateAsync(project);

        if (project.StudentId > 0)
        {
            var student = await _studentRepository.GetByIdAsync(project.StudentId);
            if (student != null)
            {
                student.AdvisorId = dto.AdvisorId;
                student.UpdatedAt = DateTime.UtcNow;
                student.UpdatedBy = _currentUser.UserId;
                await _studentRepository.UpdateAsync(student);
            }
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<AdvisorResidentsResponseDto>> GetAdvisorResidentsAsync(long id)
    {
        var advisor = await _advisorRepository.GetByIdAsync(id);
        if (advisor == null)
        {
            return Result<AdvisorResidentsResponseDto>.Failure("Asesor no encontrado o fuera del alcance de tu carrera.", 404);
        }

        var deptName = advisor.DepartmentId switch
        {
            1 => "Ingeniería Informática",
            2 => "Ingeniería Industrial",
            3 => "Ingeniería Mecatrónica",
            4 => "Ingeniería en Sistemas Computacionales",
            5 => "Ingeniería Electrónica",
            6 => "Ingeniería en Gestión Empresarial",
            _ => "Departamento Académico"
        };

        IQueryable<Student> query = _context.Students
            .Include(s => s.User)
            .Where(s => s.AdvisorId == advisor.Id && s.IsActive);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            query = query.Where(s => s.CareerId == _currentUser.CareerId.Value);
        }

        var students = await query.ToListAsync();
        var studentIds = students.Select(s => s.Id).ToList();

        var projects = await _context.Projects
            .Include(p => p.Company)
            .Where(p => studentIds.Contains(p.StudentId) && p.IsActive)
            .ToListAsync();

        var projectMap = projects
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

        var projectIds = projects.Select(p => p.Id).ToList();
        var advisoryCounts = await _context.AdvisorySessions
            .Where(a => a.AdvisorId == advisor.Id && projectIds.Contains(a.ProjectId))
            .GroupBy(a => a.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        var residentItems = students.Select(s =>
        {
            projectMap.TryGetValue(s.Id, out var proj);
            var advCount = (proj != null && advisoryCounts.TryGetValue(proj.Id, out var count)) ? count : 0;
            var fullName = $"{s.FirstName} {s.LastName}".Trim();
            var email = s.User?.Email ?? string.Empty;

            var carName = s.CareerId switch
            {
                1 => "Ing. Informática",
                2 => "Ing. Industrial",
                3 => "Ing. Mecatrónica",
                4 => "Ing. en Sistemas Computacionales",
                5 => "Ing. Electrónica",
                6 => "Ing. en Gestión Empresarial",
                _ => "Ingeniería"
            };

            return new AdvisorResidentItemDto
            {
                StudentId = s.Id,
                FullName = fullName,
                ControlNumber = s.ControlNumber,
                Email = email,
                Phone = null,
                CareerId = s.CareerId,
                CareerName = carName,
                ProjectId = proj?.Id,
                ProjectTitle = proj?.Title,
                ProjectStatus = proj?.Status.ToString(),
                CompanyName = proj?.Company?.Name,
                AdvisoryCount = advCount
            };
        }).OrderBy(r => r.FullName).ToList();

        var dto = new AdvisorResidentsResponseDto
        {
            AdvisorId = advisor.Id,
            FullName = advisor.FullName,
            Title = advisor.Title,
            Email = advisor.User?.Email ?? string.Empty,
            Phone = advisor.Phone,
            DepartmentId = advisor.DepartmentId,
            DepartmentName = deptName,
            TotalResidents = residentItems.Count,
            Residents = residentItems
        };

        return Result<AdvisorResidentsResponseDto>.Success(dto);
    }

    private static AdvisorResponseDto MapToResponseDto(Advisor advisor)
    {
        return new AdvisorResponseDto(
            advisor.Id,
            advisor.UserId,
            advisor.DepartmentId,
            advisor.AdvisorType.ToString().ToLowerInvariant(),
            advisor.FullName,
            advisor.Title,
            advisor.Phone,
            advisor.IsActive,
            advisor.CreatedAt,
            advisor.UpdatedAt,
            advisor.IsVisible,
            advisor.DisplayOrder,
            advisor.CreatedBy,
            advisor.UpdatedBy,
            advisor.DeletedBy,
            advisor.DeletedAt
        );
    }

    public async Task<Result<BatchImportResultDto>> ImportExcelAsync(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Result<BatchImportResultDto>.Failure("El archivo Excel no fue proporcionado o está vacío.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            return Result<BatchImportResultDto>.Failure("El formato del archivo debe ser .xlsx o .xls.");
        }

        using var stream = file.OpenReadStream();
        var expectedColumns = new List<string> { "Nombre", "Titulo", "Email", "Telefono", "Departamento" };
        var (isValid, errorMessage, rows) = ExcelHelper.ParseExcelFile(stream, expectedColumns);

        if (!isValid)
        {
            return Result<BatchImportResultDto>.Failure(errorMessage ?? "Error de validación de encabezados en el archivo Excel.", 400);
        }

        var result = new BatchImportResultDto
        {
            TotalRows = rows.Count
        };

        var rowNum = 1;
        foreach (var row in rows)
        {
            rowNum++;
            var name = row.GetValueOrDefault("Nombre");
            var title = row.GetValueOrDefault("Titulo");
            var email = row.GetValueOrDefault("Email");
            var phone = row.GetValueOrDefault("Telefono");
            var deptStr = row.GetValueOrDefault("Departamento");

            if (string.IsNullOrWhiteSpace(name))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El nombre completo del asesor es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El título/grado académico es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El correo institucional del asesor es obligatorio.");
                continue;
            }

            var cleanEmail = email.Trim().ToLowerInvariant();
            if (!cleanEmail.Contains('@') || !cleanEmail.Contains('.'))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El correo '{cleanEmail}' no es una dirección válida.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El teléfono de contacto del asesor es obligatorio.");
                continue;
            }

            long departmentId;
            if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
            {
                departmentId = _currentUser.CareerId.Value;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(deptStr) || !long.TryParse(deptStr.Trim(), out var parsedDept) || parsedDept < 1 || parsedDept > 6)
                {
                    result.ErrorCount++;
                    result.Errors.Add($"Fila {rowNum}: El ID de departamento es obligatorio y debe ser un número del 1 al 6 (1=INF, 2=IND, 3=MEC, 4=ISC, 5=ELE, 6=IGE).");
                    continue;
                }
                departmentId = parsedDept;
            }

            var cleanName = name.Trim();
            var cleanTitle = title.Trim();
            var cleanPhone = phone.Trim();

            // Check if user account exists
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == cleanEmail);
            long userId;

            if (existingUser != null)
            {
                userId = existingUser.Id;
                var existingAdvisor = await _advisorRepository.GetByUserIdAsync(userId);
                if (existingAdvisor != null)
                {
                    result.SkippedCount++;
                    result.Skipped.Add($"Fila {rowNum}: Omitido. Ya existe el asesor '{cleanName}' asociado al correo '{cleanEmail}'.");
                    continue;
                }
                await _roleRepository.EnsureUserRoleAsync(userId, "advisor", _currentUser.UserId);
            }
            else
            {
                var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("Docente2026!");
                var newUser = new User
                {
                    Email = cleanEmail,
                    PasswordHash = defaultPasswordHash,
                    Role = UserRole.Advisor,
                    IsActive = true,
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
                userId = newUser.Id;
                await _roleRepository.EnsureUserRoleAsync(userId, "advisor", _currentUser.UserId);
            }

            var advisor = new Advisor
            {
                UserId = userId,
                DepartmentId = departmentId,
                AdvisorType = AdvisorType.Internal,
                FullName = cleanName,
                Title = cleanTitle,
                Phone = cleanPhone,
                IsActive = true,
                IsVisible = true,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _advisorRepository.AddAsync(advisor);

            var advDept = new AdvisorDepartment
            {
                AdvisorId = advisor.Id,
                DepartmentId = departmentId,
                IsActive = true,
                IsVisible = true,
                CreatedBy = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.AdvisorDepartments.Add(advDept);
            await _context.SaveChangesAsync();

            result.SuccessCount++;
        }

        return Result<BatchImportResultDto>.Success(result);
    }
}
