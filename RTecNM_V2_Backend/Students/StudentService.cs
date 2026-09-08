using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Projects;

using TecNM.Residency.Common.EmailVerification;
using TecNM.Residency.Common.Settings;

namespace TecNM.Residency.Students;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IAdvisorRepository _advisorRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IAuthRepository _authRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailQueue _emailQueue;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ISystemSettingService _settingService;
    private readonly IEmailVerificationService _emailVerificationService;
    private readonly AppDbContext _context;

    public StudentService(
        IStudentRepository studentRepository,
        IAdvisorRepository advisorRepository,
        IProjectRepository projectRepository,
        IAuthRepository authRepository,
        IRoleRepository roleRepository,
        ICurrentUserService currentUser,
        IEmailQueue emailQueue,
        IEmailTemplateService emailTemplateService,
        ISystemSettingService settingService,
        IEmailVerificationService emailVerificationService,
        AppDbContext context)
    {
        _studentRepository = studentRepository;
        _advisorRepository = advisorRepository;
        _projectRepository = projectRepository;
        _authRepository = authRepository;
        _roleRepository = roleRepository;
        _currentUser = currentUser;
        _emailQueue = emailQueue;
        _emailTemplateService = emailTemplateService;
        _settingService = settingService;
        _emailVerificationService = emailVerificationService;
        _context = context;
    }

    public async Task<Result<PaginatedResult<StudentResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false, bool onlyApprovedProject = false, long? careerId = null)
    {
        var paged = await _studentRepository.GetPagedAsync(query, status, includeInactive, onlyApprovedProject, careerId);
        var dtos = paged.Items.Select(MapToResponseDto);
        var result = PaginatedResult<StudentResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<StudentResponseDto>>.Success(result);
    }

    public async Task<Result<byte[]>> ExportPdfAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false, bool onlyApprovedProject = false, long? careerId = null)
    {
        var students = await _studentRepository.GetAllForExportAsync(search, sortBy, sortDir, includeInactive, onlyApprovedProject, careerId);
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
        var cleanControlNum = StringSanitizer.SanitizeControlNumber(dto.ControlNumber);
        if (string.IsNullOrWhiteSpace(cleanControlNum))
            return Result<StudentResponseDto>.Failure("El número de control es obligatorio.", 400);

        var emailCheck = await _emailVerificationService.ValidateAndVerifyEmailAsync(dto.Email);
        if (!emailCheck.IsSuccess)
            return Result<StudentResponseDto>.Failure(emailCheck.ErrorMessage!, emailCheck.StatusCode ?? 400);

        var cleanEmail = emailCheck.Data!;
        var cleanFirstName = StringSanitizer.SanitizeText(dto.FirstName);
        var cleanLastName = StringSanitizer.SanitizeText(dto.LastName);
        var cleanLastName2 = string.IsNullOrWhiteSpace(dto.LastName2) ? null : StringSanitizer.SanitizeText(dto.LastName2);
        var cleanCurp = StringSanitizer.SanitizeCurp(dto.Curp);
        var cleanGender = string.IsNullOrWhiteSpace(dto.Gender) ? null : StringSanitizer.SanitizeText(dto.Gender);

        var existingStudent = await _studentRepository.GetByControlNumberAsync(cleanControlNum);
        if (existingStudent is not null)
            return Result<StudentResponseDto>.Failure("El número de control ya se encuentra registrado", 400);

        var existingUser = await _authRepository.GetByEmailAsync(cleanEmail);
        if (existingUser is not null)
            return Result<StudentResponseDto>.Failure("El correo electrónico ya está registrado", 400);

        // Default initial password is ControlNumber
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
            FirstName = cleanFirstName,
            LastName = cleanLastName,
            LastName2 = cleanLastName2,
            Curp = cleanCurp,
            Gender = cleanGender,
            CareerId = dto.CareerId,
            AcademicPeriodId = dto.AcademicPeriodId,
            Gpa = dto.Gpa,
            IsActive = true,
            CreatedBy = _currentUser.UserId,
            User = createdUser
        };

        var createdStudent = await _studentRepository.AddAsync(newStudent);

        // Enqueue Welcome Email
        var loginUrl = "http://localhost:5085/auth/login";
        var welcomeEmail = _emailTemplateService.BuildWelcomeEmail(
            $"{newStudent.FirstName} {newStudent.LastName}".Trim(),
            newStudent.ControlNumber,
            newUser.Email,
            loginUrl
        );
        _emailQueue.Enqueue(welcomeEmail);

        return Result<StudentResponseDto>.Success(MapToResponseDto(createdStudent));
    }

    public async Task<Result<StudentResponseDto>> UpdateAsync(long id, UpdateStudentDto dto)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<StudentResponseDto>.Failure("Estudiante no encontrado", 404);

        student.FirstName = StringSanitizer.SanitizeText(dto.FirstName);
        student.LastName = StringSanitizer.SanitizeText(dto.LastName);
        student.LastName2 = string.IsNullOrWhiteSpace(dto.LastName2) ? null : StringSanitizer.SanitizeText(dto.LastName2);
        student.Curp = StringSanitizer.SanitizeCurp(dto.Curp);
        student.Gender = string.IsNullOrWhiteSpace(dto.Gender) ? null : StringSanitizer.SanitizeText(dto.Gender);
        student.AcademicPeriodId = dto.AcademicPeriodId;
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            student.CareerId = _currentUser.CareerId.Value;
        }
        else if (dto.CareerId > 0)
        {
            student.CareerId = dto.CareerId;
        }
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
            return Result<StudentResponseDto>.Failure("Estudiante no encontrado o fuera del alcance de tu carrera.", 404);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && student.CareerId != _currentUser.CareerId.Value)
        {
            return Result<StudentResponseDto>.Failure("No tienes permiso para asignar asesores a estudiantes de otra carrera.", 403);
        }

        // Si ya cuenta con asesor y se intenta cambiar o desasignar, verificar vigencia máxima de 1 semana (7 días)
        if (student.AdvisorId.HasValue && student.AdvisorId.Value != advisorId)
        {
            var assignedAt = student.AdvisorAssignedAt ?? student.UpdatedAt;
            if ((DateTime.UtcNow - assignedAt).TotalDays > 7 && _currentUser.Role == UserRole.CareerHead && !_currentUser.IsInRole(UserRole.Admin))
            {
                return Result<StudentResponseDto>.Failure("No se puede cambiar el asesor asignado porque ha superado la vigencia máxima permitida de 1 semana (7 días).", 400);
            }
        }

        var advisor = await _advisorRepository.GetByIdAsync(advisorId);
        if (advisor is null)
            return Result<StudentResponseDto>.Failure("Asesor institucional no encontrado.", 404);

        student.AdvisorId = advisor.Id;
        student.Advisor = advisor;
        student.AdvisorAssignedAt = DateTime.UtcNow;
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

    public async Task<Result<StudentResponseDto>> UnassignAdvisorAsync(long studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            return Result<StudentResponseDto>.Failure("Estudiante no encontrado o fuera del alcance de tu carrera.", 404);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && student.CareerId != _currentUser.CareerId.Value)
        {
            return Result<StudentResponseDto>.Failure("No tienes permiso para desasignar asesores a estudiantes de otra carrera.", 403);
        }

        if (!student.AdvisorId.HasValue)
        {
            return Result<StudentResponseDto>.Failure("El estudiante no tiene un asesor asignado actualmente.", 400);
        }

        var assignedAt = student.AdvisorAssignedAt ?? student.UpdatedAt;
        if ((DateTime.UtcNow - assignedAt).TotalDays > 7 && _currentUser.Role == UserRole.CareerHead && !_currentUser.IsInRole(UserRole.Admin))
        {
            return Result<StudentResponseDto>.Failure("No se puede desasignar el asesor porque la asignación actual ha superado la vigencia máxima permitida de 1 semana (7 días).", 400);
        }

        student.AdvisorId = null;
        student.Advisor = null;
        student.AdvisorAssignedAt = null;
        student.UpdatedAt = DateTime.UtcNow;
        student.UpdatedBy = _currentUser.UserId;
        await _studentRepository.UpdateAsync(student);

        var project = await _projectRepository.GetByStudentIdAsync(studentId);
        if (project is not null)
        {
            project.AdvisorId = null;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = _currentUser.UserId;
            await _projectRepository.UpdateAsync(project);
        }

        return Result<StudentResponseDto>.Success(MapToResponseDto(student));
    }

    public async Task<Result<int>> BatchAssignAdvisorAsync(long advisorId, List<long> studentIds)
    {
        if (studentIds is null || studentIds.Count == 0)
            return Result<int>.Failure("Debe proporcionar al menos un estudiante para asignar.", 400);

        var advisor = await _advisorRepository.GetByIdAsync(advisorId);
        if (advisor is null)
            return Result<int>.Failure("Asesor no encontrado.", 404);

        int updatedCount = 0;
        foreach (var sid in studentIds)
        {
            var student = await _studentRepository.GetByIdAsync(sid);
            if (student is not null)
            {
                if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && student.CareerId != _currentUser.CareerId.Value)
                {
                    continue;
                }

                if (student.AdvisorId.HasValue && student.AdvisorId.Value != advisorId)
                {
                    var assignedAt = student.AdvisorAssignedAt ?? student.UpdatedAt;
                    if ((DateTime.UtcNow - assignedAt).TotalDays > 7 && _currentUser.Role == UserRole.CareerHead && !_currentUser.IsInRole(UserRole.Admin))
                    {
                        continue; // Omitir si la vigencia previa supera 1 semana
                    }
                }

                student.AdvisorId = advisor.Id;
                student.Advisor = advisor;
                student.AdvisorAssignedAt = DateTime.UtcNow;
                student.UpdatedAt = DateTime.UtcNow;
                student.UpdatedBy = _currentUser.UserId;
                await _studentRepository.UpdateAsync(student);

                var project = await _projectRepository.GetByStudentIdAsync(sid);
                if (project is not null)
                {
                    project.AdvisorId = advisor.Id;
                    project.UpdatedAt = DateTime.UtcNow;
                    project.UpdatedBy = _currentUser.UserId;
                    await _projectRepository.UpdateAsync(project);
                }

                updatedCount++;
            }
        }

        if (updatedCount == 0)
        {
            return Result<int>.Failure("No se pudo asignar el asesor a ninguno de los estudiantes seleccionados.", 400);
        }

        return Result<int>.Success(updatedCount);
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
            "Matricula", "Apellidos", "Nombre", "Sexo", "Carrera", "Semestre", "Email"
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
            var controlNum = row.GetValueOrDefault("Matricula");
            var apellidosStr = row.GetValueOrDefault("Apellidos");
            var firstName = row.GetValueOrDefault("Nombre");
            var sexoStr = row.GetValueOrDefault("Sexo");
            var carreraStr = row.GetValueOrDefault("Carrera");
            var semestreStr = row.GetValueOrDefault("Semestre");
            var emailStr = row.GetValueOrDefault("Email");

            if (string.IsNullOrWhiteSpace(controlNum))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: La matrícula (N° de Control) es obligatoria.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(firstName))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El nombre del estudiante es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(apellidosStr))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: Los apellidos del estudiante son obligatorios.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(sexoStr))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El sexo/género del estudiante es obligatorio.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(carreraStr) || !long.TryParse(carreraStr.Trim(), out var careerId) || careerId < 1 || careerId > 7)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: La columna Carrera debe contener únicamente un ID de carrera válido del 1 al 7 (1=INF, 2=IND, 3=MEC, 4=IER, 5=ELE, 6=IGE, 7=IME).");
                continue;
            }

            if (string.IsNullOrWhiteSpace(semestreStr) || !int.TryParse(semestreStr.Trim(), out var parsedSem) || parsedSem < 1)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El semestre es obligatorio y debe ser un número entero mayor o igual a 1.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(emailStr))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El correo institucional del estudiante es obligatorio.");
                continue;
            }

            var cleanControlNum = StringSanitizer.SanitizeControlNumber(controlNum);
            if (string.IsNullOrWhiteSpace(cleanControlNum))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: La matrícula (N° de Control) es obligatoria.");
                continue;
            }

            var cleanFirstName = StringSanitizer.SanitizeText(firstName);
            if (string.IsNullOrWhiteSpace(cleanFirstName))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El nombre del estudiante es obligatorio.");
                continue;
            }

            var cleanApellidos = StringSanitizer.SanitizeText(apellidosStr);
            if (string.IsNullOrWhiteSpace(cleanApellidos))
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: Los apellidos del estudiante son obligatorios.");
                continue;
            }

            // Split surnames
            string lastName1 = "SN";
            string? lastName2 = null;
            var parts = cleanApellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                lastName1 = parts[0];
            }
            else if (parts.Length == 2)
            {
                lastName1 = parts[0];
                lastName2 = parts[1];
            }
            else if (parts.Length > 2)
            {
                lastName1 = string.Join(" ", parts.Take(parts.Length - 1));
                lastName2 = parts.Last();
            }

            // Resolve and verify email (ensure valid institutional domain & DNS MX)
            var emailVerification = await _emailVerificationService.ValidateAndVerifyEmailAsync(emailStr);
            if (!emailVerification.IsSuccess)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: {emailVerification.ErrorMessage}");
                continue;
            }
            string cleanEmail = emailVerification.Data!;

            // Map Gender & Academic Semester
            var s = sexoStr.Trim().ToUpperInvariant();
            string gender = s.StartsWith("M") ? "Masculino" : s.StartsWith("F") ? "Femenino" : StringSanitizer.SanitizeText(sexoStr);
            int? periodId = parsedSem;

            var existingStudent = await _context.Students
                .Include(st => st.User)
                .FirstOrDefaultAsync(st => st.ControlNumber.ToUpper() == cleanControlNum);

            if (existingStudent != null)
            {
                // Actualizar datos del estudiante existente con la información corregida del archivo
                existingStudent.FirstName = cleanFirstName;
                existingStudent.LastName = lastName1;
                existingStudent.LastName2 = lastName2;
                existingStudent.Gender = gender;
                existingStudent.CareerId = careerId;
                existingStudent.AcademicPeriodId = periodId;
                existingStudent.IsActive = true;
                existingStudent.UpdatedAt = DateTime.UtcNow;
                existingStudent.UpdatedBy = _currentUser.UserId;

                if (existingStudent.User != null && !string.Equals(existingStudent.User.Email, cleanEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var emailOwner = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail && u.Id != existingStudent.UserId);
                    if (emailOwner == null)
                    {
                        existingStudent.User.Email = cleanEmail;
                        existingStudent.User.UpdatedAt = DateTime.UtcNow;
                        existingStudent.User.UpdatedBy = _currentUser.UserId;
                    }
                }

                await _studentRepository.UpdateAsync(existingStudent);
                result.SuccessCount++;
                continue;
            }

            var existingUser = await _authRepository.GetByEmailAsync(cleanEmail);
            if (existingUser != null)
            {
                result.SkippedCount++;
                result.Skipped.Add($"Fila {rowNum}: Omitida. Ya existe una cuenta asociada al correo '{cleanEmail}'.");
                continue;
            }

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
                FirstName = cleanFirstName,
                LastName = lastName1,
                LastName2 = lastName2,
                Gender = gender,
                CareerId = careerId,
                AcademicPeriodId = periodId,
                Gpa = 0.0m,
                IsActive = true,
                CreatedBy = _currentUser.UserId,
                User = createdUser
            };

            await _studentRepository.AddAsync(newStudent);
            result.SuccessCount++;

            // Enqueue Welcome Email
            var loginUrl = "http://localhost:5085/auth/login";
            var welcomeEmail = _emailTemplateService.BuildWelcomeEmail(
                $"{newStudent.FirstName} {lastName1}".Trim(),
                newStudent.ControlNumber,
                cleanEmail,
                loginUrl
            );
            _emailQueue.Enqueue(welcomeEmail);
        }

        return Result<BatchImportResultDto>.Success(result);
    }

    private static long MapCareerNameToId(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 1;
        var clean = name.Trim().ToUpperInvariant();
        if (long.TryParse(clean, out var id) && id >= 1 && id <= 20) return id;
        if (clean.Contains("ENERGIA") || clean.Contains("ENERGÍA") || clean.Contains("RENOVABLE") || clean == "IER" || clean.StartsWith("IER ") || clean.Contains("SISTEMA") || clean == "ISC" || clean.StartsWith("ISC ")) return 4;
        if (clean.Contains("INDUSTRIAL") || clean == "IND" || clean.StartsWith("IND ")) return 2;
        if (clean.Contains("MECATRONICA") || clean.Contains("MECATRÓNICA") || clean == "MEC" || clean.StartsWith("MEC ")) return 3;
        if (clean.Contains("MECANICA") || clean.Contains("MECÁNICA") || clean == "IME" || clean.StartsWith("IME ")) return 7;
        if (clean.Contains("INFORMATICA") || clean.Contains("INFORMÁTICA") || clean == "INF" || clean.StartsWith("INF ")) return 1;
        if (clean.Contains("ELECTRONICA") || clean.Contains("ELECTRÓNICA") || clean == "ELE" || clean.StartsWith("ELE ")) return 5;
        if (clean.Contains("GESTION") || clean.Contains("GESTIÓN") || clean.Contains("EMPRESARIAL") || clean == "IGE" || clean.StartsWith("IGE ")) return 6;
        return 1;
    }

    public async Task<Result<int>> SendMassPresentationLettersAsync()
    {
        var unsentStudents = await _context.Students
            .Include(s => s.User)
            .Where(s => s.IsActive && !s.IsPresentationLetterSent)
            .ToListAsync();

        if (unsentStudents.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var activeTemplateHtml = await _settingService.GetPresentationLetterTemplateAsync();

        int sentCount = 0;
        foreach (var student in unsentStudents)
        {
            var email = student.User?.Email;
            if (string.IsNullOrWhiteSpace(email)) continue;

            var careerName = GetCareerNameById(student.CareerId);
            var studentName = $"{student.FirstName} {student.LastName} {student.LastName2}".Trim();

            var project = await _context.Projects
                .Include(p => p.Company)
                .FirstOrDefaultAsync(p => p.StudentId == student.Id && p.IsActive);

            var companyName = project?.Company?.Name ?? "A QUIEN CORRESPONDA";

            var letterData = new PresentationLetterData
            {
                StudentFullName = studentName,
                ControlNumber = student.ControlNumber,
                CareerName = careerName,
                CompanyName = companyName,
                FolioNumber = $"TecNM-MON-VP-{DateTime.UtcNow.Year}-{student.ControlNumber}",
                IssueDate = DateTime.UtcNow
            };

            var pdfBytes = PresentationLetterPdfService.GeneratePresentationLetterPdf(letterData, activeTemplateHtml);
            var emailMsg = _emailTemplateService.BuildPresentationLetterEmail(
                studentName,
                student.ControlNumber,
                email,
                careerName,
                companyName,
                pdfBytes
            );

            _emailQueue.Enqueue(emailMsg);

            student.IsPresentationLetterSent = true;
            student.PresentationLetterSentAt = DateTime.UtcNow;
            student.UpdatedAt = DateTime.UtcNow;
            student.UpdatedBy = _currentUser.UserId;

            sentCount++;
        }

        await _context.SaveChangesAsync();
        return Result<int>.Success(sentCount);
    }

    public async Task<Result<bool>> SendPresentationLetterAsync(long studentId)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == studentId && s.IsActive);

        if (student is null)
            return Result<bool>.Failure("Estudiante no encontrado.", 404);

        var email = student.User?.Email;
        if (string.IsNullOrWhiteSpace(email))
            return Result<bool>.Failure("El estudiante no tiene un correo electrónico configurado.", 400);

        var careerName = GetCareerNameById(student.CareerId);
        var studentName = $"{student.FirstName} {student.LastName} {student.LastName2}".Trim();

        var project = await _context.Projects
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.StudentId == student.Id && p.IsActive);

        var companyName = project?.Company?.Name ?? "A QUIEN CORRESPONDA";

        var letterData = new PresentationLetterData
        {
            StudentFullName = studentName,
            ControlNumber = student.ControlNumber,
            CareerName = careerName,
            CompanyName = companyName,
            FolioNumber = $"TecNM-MON-VP-{DateTime.UtcNow.Year}-{student.ControlNumber}",
            IssueDate = DateTime.UtcNow
        };

        var activeTemplateHtml = await _settingService.GetPresentationLetterTemplateAsync();
        var pdfBytes = PresentationLetterPdfService.GeneratePresentationLetterPdf(letterData, activeTemplateHtml);
        var emailMsg = _emailTemplateService.BuildPresentationLetterEmail(
            studentName,
            student.ControlNumber,
            email,
            careerName,
            companyName,
            pdfBytes
        );

        _emailQueue.Enqueue(emailMsg);

        student.IsPresentationLetterSent = true;
        student.PresentationLetterSentAt = DateTime.UtcNow;
        student.UpdatedAt = DateTime.UtcNow;
        student.UpdatedBy = _currentUser.UserId;

        await _context.SaveChangesAsync();
        return Result<bool>.Success(true);
    }

    public async Task<Result<byte[]>> GetPresentationLetterPdfAsync(long studentId)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == studentId && s.IsActive);

        if (student is null)
            return Result<byte[]>.Failure("Estudiante no encontrado.", 404);

        var careerName = GetCareerNameById(student.CareerId);
        var studentName = $"{student.FirstName} {student.LastName} {student.LastName2}".Trim();

        var project = await _context.Projects
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.StudentId == student.Id && p.IsActive);

        var companyName = project?.Company?.Name ?? "A QUIEN CORRESPONDA";

        var letterData = new PresentationLetterData
        {
            StudentFullName = studentName,
            ControlNumber = student.ControlNumber,
            CareerName = careerName,
            CompanyName = companyName,
            FolioNumber = $"TecNM-MON-VP-{DateTime.UtcNow.Year}-{student.ControlNumber}",
            IssueDate = DateTime.UtcNow
        };

        var activeTemplateHtml = await _settingService.GetPresentationLetterTemplateAsync();
        var pdfBytes = PresentationLetterPdfService.GeneratePresentationLetterPdf(letterData, activeTemplateHtml);
        return Result<byte[]>.Success(pdfBytes);
    }

    private static string GetCareerNameById(long careerId)
    {
        return careerId switch
        {
            1 => "Ingeniería Informática",
            2 => "Ingeniería Industrial",
            3 => "Ingeniería Mecatrónica",
            4 => "Ingeniería en Energías Renovables",
            5 => "Ingeniería Electrónica",
            6 => "Ingeniería en Gestión Empresarial",
            7 => "Ingeniería Mecánica",
            _ => "Ingeniería"
        };
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
            AdvisorAssignedAt = student.AdvisorAssignedAt,
            AdvisorName = student.Advisor?.FullName,
            AcademicPeriodId = student.AcademicPeriodId,
            Email = student.User?.Email ?? string.Empty,
            Gpa = student.Gpa,
            IsPresentationLetterSent = student.IsPresentationLetterSent,
            PresentationLetterSentAt = student.PresentationLetterSentAt,
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
