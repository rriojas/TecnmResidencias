using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Students;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IAdvisorRepository _advisorRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUser;

    public StudentService(
        IStudentRepository studentRepository,
        IAdvisorRepository advisorRepository,
        IProjectRepository projectRepository,
        IAuthRepository authRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUser)
    {
        _studentRepository = studentRepository;
        _advisorRepository = advisorRepository;
        _projectRepository = projectRepository;
        _authRepository = authRepository;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
    }

    public async Task<Result<PaginatedResult<StudentResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        var paged = await _studentRepository.GetPagedAsync(query, status, includeInactive);
        var dtos = paged.Items.Select(MapToResponseDto);
        var result = PaginatedResult<StudentResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<StudentResponseDto>>.Success(result);
    }

    public async Task<Result<byte[]>> ExportPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        var students = await _studentRepository.GetAllForExportAsync(search, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Directorio de Estudiantes Residentes - TecNM Campus Monclova",
            Headers = new List<string> { "No. Control", "Nombre", "Correo", "Carrera", "Promedio", "Estado", "Creado el", "Actualizado el" },
            Rows = students.Select(s => new List<string>
            {
                s.ControlNumber,
                $"{s.FirstName} {s.LastName} {s.LastName2 ?? ""}".Trim(),
                s.User?.Email ?? string.Empty,
                s.CareerId.ToString(),
                s.Gpa.ToString("0.0"),
                s.IsActive ? "Activo" : "Inactivo",
                s.CreatedAt.ToString("dd/MM/yyyy"),
                s.UpdatedAt.ToString("dd/MM/yyyy")
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    public async Task<Result<List<StudentOptionDto>>> GetOptionsAsync()
    {
        var students = await _studentRepository.GetOptionsAsync();
        var dtos = students.Select(s => new StudentOptionDto
        {
            Id = s.Id,
            ControlNumber = s.ControlNumber,
            FullName = $"{s.FirstName} {s.LastName}".Trim()
        }).ToList();

        return Result<List<StudentOptionDto>>.Success(dtos);
    }

    public async Task<Result<StudentResponseDto>> GetByIdAsync(long id)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<StudentResponseDto>.Failure("Estudiante no encontrado", 404);

        return Result<StudentResponseDto>.Success(MapToResponseDto(student));
    }

    public async Task<Result<StudentResponseDto>> GetMeAsync(long userId)
    {
        if (userId <= 0)
            return Result<StudentResponseDto>.Failure("Sesión no autenticada.", 401);

        var student = await _studentRepository.GetByUserIdAsync(userId);
        if (student is null)
            return Result<StudentResponseDto>.Failure("No se encontró un perfil de estudiante asociado a tu cuenta.", 404);

        return Result<StudentResponseDto>.Success(MapToResponseDto(student));
    }

    public async Task<Result<StudentResponseDto>> CreateAsync(CreateStudentDto dto)
    {
        if (!InstitutionalEmail.IsValid(dto.Email))
            return Result<StudentResponseDto>.Failure(InstitutionalEmail.ErrorMessage, 400);

        var cleanControlNum = (dto.ControlNumber ?? "").Trim().ToUpperInvariant();
        var existingStudent = await _studentRepository.GetByControlNumberAsync(cleanControlNum);
        if (existingStudent is not null)
            return Result<StudentResponseDto>.Failure("El número de control ya se encuentra registrado", 400);

        var existingUser = await _authRepository.GetByEmailAsync(dto.Email);
        if (existingUser is not null)
            return Result<StudentResponseDto>.Failure("El correo electrónico ya está registrado", 400);

        // Default initial password is ControlNumber
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(cleanControlNum);
        var newUser = new User
        {
            Email = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = defaultPasswordHash,
            Role = UserRole.Student,
            IsActive = true,
            CreatedBy = _currentUser.UserId
        };

        var createdUser = await _authRepository.AddUserAsync(newUser);

        await _roleRepository.EnsureUserRoleAsync(createdUser.Id, "student", _currentUser.UserId);

        var newStudent = new Student
        {
            UserId = createdUser.Id,
            ControlNumber = cleanControlNum,
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            LastName2 = !string.IsNullOrWhiteSpace(dto.LastName2) ? dto.LastName2.Trim() : null,
            Curp = !string.IsNullOrWhiteSpace(dto.Curp) ? dto.Curp.Trim().ToUpperInvariant() : null,
            Gender = !string.IsNullOrWhiteSpace(dto.Gender) ? dto.Gender.Trim() : null,
            CareerId = dto.CareerId,
            AcademicPeriodId = dto.AcademicPeriodId,
            Gpa = dto.Gpa,
            IsActive = true,
            CreatedBy = _currentUser.UserId,
            User = createdUser
        };

        var createdStudent = await _studentRepository.AddAsync(newStudent);
        return Result<StudentResponseDto>.Success(MapToResponseDto(createdStudent));
    }

    public async Task<Result<StudentResponseDto>> UpdateAsync(long id, UpdateStudentDto dto)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<StudentResponseDto>.Failure("Estudiante no encontrado", 404);

        student.FirstName = dto.FirstName.Trim();
        student.LastName = dto.LastName.Trim();
        student.LastName2 = !string.IsNullOrWhiteSpace(dto.LastName2) ? dto.LastName2.Trim() : null;
        student.Curp = !string.IsNullOrWhiteSpace(dto.Curp) ? dto.Curp.Trim().ToUpperInvariant() : null;
        student.Gender = !string.IsNullOrWhiteSpace(dto.Gender) ? dto.Gender.Trim() : null;
        student.AcademicPeriodId = dto.AcademicPeriodId;
        student.CareerId = dto.CareerId;
        student.Gpa = dto.Gpa;
        student.UpdatedAt = DateTime.UtcNow;
        student.UpdatedBy = _currentUser.UserId;

        await _studentRepository.UpdateAsync(student);
        return Result<StudentResponseDto>.Success(MapToResponseDto(student));
    }

    public async Task<Result<bool>> SoftDeleteAsync(long id, long deletedByUserId)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<bool>.Failure("Estudiante no encontrado", 404);

        student.IsActive = false;
        student.DeletedAt = DateTime.UtcNow;
        student.DeletedBy = deletedByUserId;

        if (student.User is not null)
        {
            student.User.IsActive = false;
            student.User.DeletedAt = DateTime.UtcNow;
            student.User.DeletedBy = deletedByUserId;
        }

        await _studentRepository.UpdateAsync(student);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ReactivateAsync(long id)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<bool>.Failure("Estudiante no encontrado", 404);

        student.IsActive = true;
        student.DeletedAt = null;
        student.DeletedBy = null;

        if (student.User is not null)
        {
            student.User.IsActive = true;
            student.User.DeletedAt = null;
            student.User.DeletedBy = null;
        }

        await _studentRepository.UpdateAsync(student);
        return Result<bool>.Success(true);
    }

    public async Task<Result<StudentResponseDto>> AssignAdvisorAsync(long studentId, long advisorId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            return Result<StudentResponseDto>.Failure("Estudiante no encontrado.", 404);

        var advisor = await _advisorRepository.GetByIdAsync(advisorId);
        if (advisor is null)
            return Result<StudentResponseDto>.Failure("Asesor no encontrado.", 404);

        student.AdvisorId = advisor.Id;
        student.Advisor = advisor;
        student.UpdatedAt = DateTime.UtcNow;
        student.UpdatedBy = _currentUser.UserId;

        await _studentRepository.UpdateAsync(student);

        var project = await _projectRepository.GetByStudentIdAsync(studentId);
        if (project is not null)
        {
            project.AdvisorId = advisor.Id;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = _currentUser.UserId;
            await _projectRepository.UpdateAsync(project);
        }

        return Result<StudentResponseDto>.Success(MapToResponseDto(student));
    }

    public async Task<Result<BatchImportResultDto>> ImportExcelAsync(Microsoft.AspNetCore.Http.IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return Result<BatchImportResultDto>.Failure("Debe seleccionar un archivo Excel válido.");
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
        {
            return Result<BatchImportResultDto>.Failure("El archivo debe ser un documento Excel con extensión .xlsx o .xls.");
        }

        var expectedColumns = new List<string>
        {
            "NumeroControl", "Nombre", "ApellidoPaterno", "ApellidoMaterno", "Correo", "CURP", "Genero", "CarreraId", "PeriodoAcademicoId", "Promedio"
        };

        using var stream = file.OpenReadStream();
        var (isValid, errorMessage, rows) = ExcelHelper.ParseExcelFile(stream, expectedColumns);

        if (!isValid)
        {
            return Result<BatchImportResultDto>.Failure(errorMessage ?? "Error de validación de encabezados en el archivo Excel.", 400);
        }

        var result = new BatchImportResultDto
        {
            TotalRows = rows.Count
        };

        int rowNum = 1;
        foreach (var row in rows)
        {
            rowNum++;
            var controlNum = row.GetValueOrDefault("NumeroControl");
            var firstName = row.GetValueOrDefault("Nombre");
            var lastName1 = row.GetValueOrDefault("ApellidoPaterno");
            var lastName2 = row.GetValueOrDefault("ApellidoMaterno");
            var email = row.GetValueOrDefault("Correo");
            var curp = row.GetValueOrDefault("CURP");
            var gender = row.GetValueOrDefault("Genero");
            var careerStr = row.GetValueOrDefault("CarreraId");
            var periodStr = row.GetValueOrDefault("PeriodoAcademicoId");
            var gpaStr = row.GetValueOrDefault("Promedio");

            if (string.IsNullOrWhiteSpace(controlNum))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El número de control es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName1))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El nombre y el primer apellido son obligatorios.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(email) || !InstitutionalEmail.IsValid(email))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El correo institucional '{email}' no es válido.");
                continue;
            }

            var cleanControlNum = controlNum.Trim().ToUpperInvariant();
            var cleanEmail = email.Trim().ToLowerInvariant();

            var existingStudent = await _studentRepository.GetByControlNumberAsync(cleanControlNum);
            if (existingStudent != null)
            {
                result.SkippedCount++;
                result.Skipped.Add($"Fila {rowNum}: Omitida. Ya existe un estudiante con N° Control '{cleanControlNum}'.");
                continue;
            }

            var existingUser = await _authRepository.GetByEmailAsync(cleanEmail);
            if (existingUser != null)
            {
                result.SkippedCount++;
                result.Skipped.Add($"Fila {rowNum}: Omitida. Ya existe una cuenta asociada al correo '{cleanEmail}'.");
                continue;
            }

            long.TryParse(careerStr, out var careerId);
            if (careerId <= 0) careerId = 1;

            int? periodId = null;
            if (int.TryParse(periodStr, out var parsedPeriod) && parsedPeriod > 0)
            {
                periodId = parsedPeriod;
            }

            decimal.TryParse(gpaStr, out var gpa);

            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword(cleanControlNum);
            var newUser = new User
            {
                Email = cleanEmail,
                PasswordHash = defaultPasswordHash,
                Role = UserRole.Student,
                IsActive = true,
                CreatedBy = _currentUser.UserId
            };

            var createdUser = await _authRepository.AddUserAsync(newUser);
            await _roleRepository.EnsureUserRoleAsync(createdUser.Id, "student", _currentUser.UserId);

            var newStudent = new Student
            {
                UserId = createdUser.Id,
                ControlNumber = cleanControlNum,
                FirstName = firstName.Trim(),
                LastName = lastName1.Trim(),
                LastName2 = !string.IsNullOrWhiteSpace(lastName2) ? lastName2.Trim() : null,
                Curp = !string.IsNullOrWhiteSpace(curp) ? curp.Trim().ToUpperInvariant() : null,
                Gender = !string.IsNullOrWhiteSpace(gender) ? gender.Trim() : null,
                CareerId = careerId,
                AcademicPeriodId = periodId,
                Gpa = gpa,
                IsActive = true,
                CreatedBy = _currentUser.UserId,
                User = createdUser
            };

            await _studentRepository.AddAsync(newStudent);
            result.SuccessCount++;
        }

        return Result<BatchImportResultDto>.Success(result);
    }

    private static StudentResponseDto MapToResponseDto(Student student)
    {
        return new StudentResponseDto
        {
            Id = student.Id,
            UserId = student.UserId,
            ControlNumber = student.ControlNumber,
            FirstName = student.FirstName,
            LastName = student.LastName,
            LastName2 = student.LastName2,
            Curp = student.Curp,
            Gender = student.Gender,
            CareerId = student.CareerId,
            AdvisorId = student.AdvisorId,
            AdvisorName = student.Advisor?.FullName,
            AcademicPeriodId = student.AcademicPeriodId,
            Email = student.User?.Email ?? string.Empty,
            Gpa = student.Gpa,
            IsActive = student.IsActive,
            IsVisible = student.IsVisible,
            DisplayOrder = student.DisplayOrder,
            CreatedAt = student.CreatedAt,
            UpdatedAt = student.UpdatedAt,
            CreatedBy = student.CreatedBy,
            UpdatedBy = student.UpdatedBy,
            DeletedBy = student.DeletedBy,
            DeletedAt = student.DeletedAt
        };
    }
}
