using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Documents;
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

    public async Task<Result<PaginatedResult<StudentResponseDto>>> GetPagedAsync(
        PaginationQuery query,
        string? status,
        bool includeInactive = false,
        bool onlyApprovedProject = false,
        long? careerId = null,
        bool excludeEvaluated = false,
        string? assignmentStatus = null,
        string? acceptanceLetterStatus = null,
        string? residencyStage = null)
    {
        var paged = await _studentRepository.GetPagedAsync(query, status, includeInactive, onlyApprovedProject, careerId, excludeEvaluated, assignmentStatus, acceptanceLetterStatus, residencyStage);
        var studentIds = paged.Items.Select(s => s.Id).ToList();

        var projects = await _context.Projects
            .Where(p => studentIds.Contains(p.StudentId) && p.IsActive)
            .Select(p => new { p.Id, p.StudentId, p.Title, p.ProjectType, p.Status, p.AdvisorId })
            .ToListAsync();

        var projectIds = projects.Select(p => p.Id).ToList();
        var projectDocuments = await _context.Documents
            .Where(d => projectIds.Contains(d.ProjectId) && d.IsActive &&
                (d.DocumentType == DocumentType.CartaAceptacion ||
                 d.DocumentType == DocumentType.CartaAprobacion ||
                 d.DocumentType == DocumentType.ConstanciaAcreditacion ||
                 d.DocumentType == DocumentType.Formato29 ||
                 d.DocumentType == DocumentType.Formato29V2 ||
                 d.DocumentType == DocumentType.Formato30))
            .OrderByDescending(d => d.Id)
            .ToListAsync();

        var docsByProject = projectDocuments
            .GroupBy(d => d.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var projectByStudent = projects
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Id).First());

        var careerIds = paged.Items.Select(s => s.CareerId).Distinct().ToList();
        var careers = await _context.Careers
            .Where(c => careerIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var dtos = paged.Items.Select(s =>
        {
            var dto = MapToResponseDto(s);
            dto.FullName = $"{s.FirstName} {s.LastName} {s.LastName2}".Trim().Replace("  ", " ");
            dto.Formato29Deadline = s.Formato29Deadline;
            dto.Formato30Deadline = s.Formato30Deadline;
            if (careers.TryGetValue(s.CareerId, out var cName))
            {
                dto.CareerName = cName;
            }

            if (projectByStudent.TryGetValue(s.Id, out var proj))
            {
                dto.HasProject = true;
                dto.ProjectTitle = proj.Title;
                dto.ProjectType = proj.ProjectType;
                dto.ProjectStatus = proj.Status.ToString().ToLowerInvariant();

                bool isInnovatec = !string.IsNullOrWhiteSpace(proj.ProjectType) && proj.ProjectType.Contains("innovatec", StringComparison.OrdinalIgnoreCase);
                bool isHackatec = !string.IsNullOrWhiteSpace(proj.ProjectType) && proj.ProjectType.Contains("hackatec", StringComparison.OrdinalIgnoreCase);
                bool isAccreditation = isInnovatec || isHackatec || (proj.ProjectType != null && proj.ProjectType.StartsWith("acreditacion", StringComparison.OrdinalIgnoreCase));

                dto.IsAccreditation = isAccreditation;
                if (isInnovatec)
                {
                    dto.ExemptionReason = "Omisión InnovaTecNM";
                }
                else if (isHackatec)
                {
                    dto.ExemptionReason = "Omisión HackaTec";
                }
                else if (isAccreditation)
                {
                    dto.ExemptionReason = "Omisión por Acreditación";
                }

                if (docsByProject.TryGetValue(proj.Id, out var pDocs))
                {
                    var acceptanceDoc = pDocs.FirstOrDefault(d => 
                        d.DocumentType == DocumentType.CartaAceptacion ||
                        d.DocumentType == DocumentType.CartaAprobacion ||
                        d.DocumentType == DocumentType.ConstanciaAcreditacion);

                    if (acceptanceDoc != null)
                    {
                        dto.HasAcceptanceLetter = true;
                        dto.AcceptanceLetterStatus = acceptanceDoc.Status.ToString().ToLowerInvariant();
                    }

                    var f29 = pDocs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29, StringComparison.OrdinalIgnoreCase));
                    var f29v2 = pDocs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29V2, StringComparison.OrdinalIgnoreCase));
                    var f30 = pDocs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato30, StringComparison.OrdinalIgnoreCase));

                    dto.Formato29Status = f29?.Status ?? "not_uploaded";
                    dto.Formato29V2Status = f29v2?.Status ?? "not_uploaded";
                    dto.Formato30Status = f30?.Status ?? "not_uploaded";

                    bool f29Approved = f29 != null && string.Equals(f29.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);
                    dto.CanUploadSecondPhase = f29Approved;

                    bool isDocBlocked = false;
                    if (s.Formato29Deadline.HasValue && DateTime.UtcNow > s.Formato29Deadline.Value && !f29Approved)
                    {
                        isDocBlocked = true;
                    }
                    else if (s.Formato30Deadline.HasValue && DateTime.UtcNow > s.Formato30Deadline.Value)
                    {
                        bool f29v2Approved = f29v2 != null && string.Equals(f29v2.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);
                        bool f30Approved = f30 != null && string.Equals(f30.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);
                        if (!f29v2Approved || !f30Approved)
                        {
                            isDocBlocked = true;
                        }
                    }
                    dto.IsDocumentBlocked = isDocBlocked;
                }
                else
                {
                    dto.Formato29Status = "not_uploaded";
                    dto.Formato29V2Status = "not_uploaded";
                    dto.Formato30Status = "not_uploaded";
                    dto.CanUploadSecondPhase = false;
                    if (s.Formato29Deadline.HasValue && DateTime.UtcNow > s.Formato29Deadline.Value)
                    {
                        dto.IsDocumentBlocked = true;
                    }
                }

                // Determinar Estado de las Residencias
                if (proj.Status == ProjectStatus.Completed)
                {
                    dto.ResidencyStage = "Concluido / Evaluado";
                }
                else if (s.AdvisorId.HasValue && (proj.Status == ProjectStatus.Approved || proj.Status == ProjectStatus.InProgress))
                {
                    dto.ResidencyStage = "En Residencia";
                }
                else if (s.AdvisorId.HasValue)
                {
                    dto.ResidencyStage = "Asesor Asignado";
                }
                else if (proj.Status == ProjectStatus.Approved)
                {
                    dto.ResidencyStage = "Dictamen Aprobado";
                }
                else if (proj.Status == ProjectStatus.Rejected)
                {
                    dto.ResidencyStage = "Con Observaciones";
                }
                else if (isAccreditation)
                {
                    dto.ResidencyStage = isInnovatec ? "Acreditación InnovaTecNM" : (isHackatec ? "Acreditación HackaTec" : "Acreditación");
                }
                else if (dto.HasAcceptanceLetter)
                {
                    dto.ResidencyStage = "Carta Cargada";
                }
                else if (proj.Status == ProjectStatus.Pending || proj.Status == ProjectStatus.Proposed || proj.Status == ProjectStatus.UnderReview)
                {
                    dto.ResidencyStage = "Anteproyecto Registrado";
                }
                else if (proj.Status == ProjectStatus.Draft)
                {
                    dto.ResidencyStage = "Borrador";
                }
                else
                {
                    dto.ResidencyStage = "En Proceso";
                }
            }
            else
            {
                dto.HasProject = false;
                dto.ResidencyStage = "Sin Anteproyecto";
            }

            return dto;
        });

        var result = PaginatedResult<StudentResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<StudentResponseDto>>.Success(result);
    }

    public async Task<Result<byte[]>> ExportPdfAsync(
        string? search,
        string? sortBy,
        string? sortDir,
        bool includeInactive = false,
        bool onlyApprovedProject = false,
        long? careerId = null,
        bool excludeEvaluated = false,
        string? assignmentStatus = null,
        string? acceptanceLetterStatus = null,
        string? residencyStage = null)
    {
        var students = await _studentRepository.GetAllForExportAsync(search, sortBy, sortDir, includeInactive, onlyApprovedProject, careerId, excludeEvaluated, assignmentStatus, acceptanceLetterStatus, residencyStage);
        var definition = new PdfTableDefinition
        {
            Title = "Directorio de Estudiantes Residentes - TecNM Campus Monclova",
            Headers = new List<string> { "No. Control", "Nombre", "Género", "Correo", "Carrera", "Promedio", "Estado", "Creado el", "Actualizado el" },
            Rows = students.Select(s => new List<string>
            {
                s.ControlNumber,
                $"{s.FirstName} {s.LastName} {s.LastName2 ?? ""}".Trim(),
                s.Gender ?? "—",
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

        var dto = MapToResponseDto(student);
        var career = await _context.Careers.FirstOrDefaultAsync(c => c.Id == student.CareerId);
        dto.CareerName = career?.Name;
        var project = await _projectRepository.GetByStudentIdAsync(id);
        await EnrichWithProjectDetailsAsync(dto, student, project);

        return Result<StudentResponseDto>.Success(dto);
    }

    public async Task<Result<StudentResponseDto>> GetMeAsync(long userId)
    {
        if (userId <= 0)
            return Result<StudentResponseDto>.Failure("Sesión no autenticada.", 401);

        var student = await _studentRepository.GetByUserIdAsync(userId);
        if (student is null)
            return Result<StudentResponseDto>.Failure("No se encontró un perfil de estudiante asociado a tu cuenta.", 404);

        var dto = MapToResponseDto(student);
        var project = await _projectRepository.GetByStudentIdAsync(student.Id);
        await EnrichWithProjectDetailsAsync(dto, student, project);

        return Result<StudentResponseDto>.Success(dto);
    }

    private async Task EnrichWithProjectDetailsAsync(StudentResponseDto dto, Student student, Project? project)
    {
        if (project != null && project.IsActive)
        {
            dto.HasProject = true;
            dto.ProjectId = project.Id;
            dto.ProjectTitle = project.Title;
            dto.ProjectType = project.ProjectType;
            dto.ProjectStatus = project.Status.ToString().ToLowerInvariant();

            bool isInnovatec = !string.IsNullOrWhiteSpace(project.ProjectType) && project.ProjectType.Contains("innovatec", StringComparison.OrdinalIgnoreCase);
            bool isHackatec = !string.IsNullOrWhiteSpace(project.ProjectType) && project.ProjectType.Contains("hackatec", StringComparison.OrdinalIgnoreCase);
            bool isAccreditation = isInnovatec || isHackatec || (project.ProjectType != null && project.ProjectType.StartsWith("acreditacion", StringComparison.OrdinalIgnoreCase));
            dto.IsAccreditation = isAccreditation;
            if (isInnovatec) dto.ExemptionReason = "Omisión InnovaTecNM";
            else if (isHackatec) dto.ExemptionReason = "Omisión HackaTec";
            else if (isAccreditation) dto.ExemptionReason = "Omisión por Acreditación";

            var doc = await _context.Documents.FirstOrDefaultAsync(d =>
                d.ProjectId == project.Id && d.IsActive &&
                (d.DocumentType == DocumentType.CartaAceptacion ||
                 d.DocumentType == DocumentType.CartaAprobacion ||
                 d.DocumentType == DocumentType.ConstanciaAcreditacion));

            if (doc != null)
            {
                dto.HasAcceptanceLetter = true;
                dto.AcceptanceLetterStatus = doc.Status.ToString().ToLowerInvariant();
            }

            // Determinar Estado de las Residencias
            if (project.Status == ProjectStatus.Completed)
            {
                dto.ResidencyStage = "Concluido / Evaluado";
            }
            else if (student.AdvisorId.HasValue && (project.Status == ProjectStatus.Approved || project.Status == ProjectStatus.InProgress))
            {
                dto.ResidencyStage = "En Residencia";
            }
            else if (student.AdvisorId.HasValue)
            {
                dto.ResidencyStage = "Asesor Asignado";
            }
            else if (project.Status == ProjectStatus.Approved)
            {
                dto.ResidencyStage = "Dictamen Aprobado";
            }
            else if (project.Status == ProjectStatus.Rejected)
            {
                dto.ResidencyStage = "Con Observaciones";
            }
            else if (isAccreditation)
            {
                dto.ResidencyStage = isInnovatec ? "Acreditación InnovaTecNM" : (isHackatec ? "Acreditación HackaTec" : "Acreditación");
            }
            else if (dto.HasAcceptanceLetter)
            {
                dto.ResidencyStage = "Carta Cargada";
            }
            else if (project.Status == ProjectStatus.Pending || project.Status == ProjectStatus.Proposed || project.Status == ProjectStatus.UnderReview)
            {
                dto.ResidencyStage = "Anteproyecto Registrado";
            }
            else if (project.Status == ProjectStatus.Draft)
            {
                dto.ResidencyStage = "Borrador";
            }
            else
            {
                dto.ResidencyStage = "En Proceso";
            }
        }
        else
        {
            dto.HasProject = false;
            dto.ResidencyStage = "Sin Anteproyecto";
        }
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
            HasComplementaryActivities = dto.HasComplementaryActivities,
            HasSocialService = dto.HasSocialService,
            HasSpecialRequirements = dto.HasSpecialRequirements,
            IsActive = true,
            CreatedBy = _currentUser.UserId,
            User = createdUser
        };

        var createdStudent = await _studentRepository.AddAsync(newStudent);

        // Check requirements and auto-block if any is false
        await CheckAndApplyAutoBlockAsync(createdStudent);

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

    private async Task CheckAndApplyAutoBlockAsync(Student student)
    {
        var failedRequirements = new List<string>();

        if (!student.HasComplementaryActivities)
            failedRequirements.Add("Actividades Complementarias");
        if (!student.HasSocialService)
            failedRequirements.Add("Servicio Social");
        if (!student.HasSpecialRequirements)
            failedRequirements.Add("Especiales");

        var activeBlock = await _studentRepository.GetActiveBlockAsync(student.Id);

        if (failedRequirements.Count > 0)
        {
            var reason = $"El estudiante no cumple con los siguientes requisitos obligatorios: {string.Join(", ", failedRequirements)}. No podrá acceder al sistema hasta regularizar su situación.";
            if (activeBlock == null)
            {
                var block = new StudentBlock
                {
                    StudentId = student.Id,
                    Reason = reason,
                    BlockedBy = _currentUser.UserId,
                    BlockedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _studentRepository.AddBlockAsync(block);
            }
            else if (!activeBlock.Reason.StartsWith("[Bloqueo manual]"))
            {
                activeBlock.Reason = reason;
                activeBlock.BlockedAt = DateTime.UtcNow;
                activeBlock.BlockedBy = _currentUser.UserId;
                await _studentRepository.UpdateBlockAsync(activeBlock);
            }
        }
        else
        {
            if (activeBlock != null && !activeBlock.Reason.StartsWith("[Bloqueo manual]"))
            {
                activeBlock.IsActive = false;
                activeBlock.UnblockedAt = DateTime.UtcNow;
                activeBlock.UnblockedBy = _currentUser.UserId;
                await _studentRepository.UpdateBlockAsync(activeBlock);
            }
        }
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
        student.HasComplementaryActivities = dto.HasComplementaryActivities;
        student.HasSocialService = dto.HasSocialService;
        student.HasSpecialRequirements = dto.HasSpecialRequirements;
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

        // Re-check requirements on update
        await CheckAndApplyAutoBlockAsync(student);

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

        var project = await _projectRepository.GetByStudentIdAsync(studentId);
        if (project is null || !project.IsActive)
        {
            return Result<StudentResponseDto>.Failure("No se puede asignar un asesor sin que el estudiante cuente con un anteproyecto registrado.", 400);
        }

        bool isAccreditation = project.ProjectType is "acreditacion_innovatec" or "acreditacion_hackatec"
            || (!string.IsNullOrWhiteSpace(project.ProjectType) && (project.ProjectType.Contains("innovatec", StringComparison.OrdinalIgnoreCase) || project.ProjectType.Contains("hackatec", StringComparison.OrdinalIgnoreCase)));
        bool hasValidDoc = false;

        if (isAccreditation)
        {
            // Para eventos institucionales (InnovaTecNM / HackaTec), la carta de aceptación es omitida por acreditación
            hasValidDoc = true;
        }
        else
        {
            hasValidDoc = await _context.Documents.AnyAsync(d =>
                d.ProjectId == project.Id &&
                d.IsActive &&
                (d.DocumentType == DocumentType.CartaAceptacion || d.DocumentType == DocumentType.CartaAprobacion));
        }

        if (!hasValidDoc)
        {
            return Result<StudentResponseDto>.Failure("No se puede asignar un asesor: el anteproyecto no cuenta con carta de aceptación oficial registrada.", 400);
        }

        student.AdvisorId = advisor.Id;
        student.Advisor = advisor;
        student.AdvisorAssignedAt = DateTime.UtcNow;
        student.UpdatedAt = DateTime.UtcNow;
        student.UpdatedBy = _currentUser.UserId;

        await _studentRepository.UpdateAsync(student);

        project.AdvisorId = advisor.Id;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;
        await _projectRepository.UpdateAsync(project);

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

                var project = await _projectRepository.GetByStudentIdAsync(sid);
                if (project is null || !project.IsActive)
                {
                    continue; // Omitir: requiere anteproyecto
                }

                bool isAccreditation = project.ProjectType is "acreditacion_innovatec" or "acreditacion_hackatec"
                    || (!string.IsNullOrWhiteSpace(project.ProjectType) && (project.ProjectType.Contains("innovatec", StringComparison.OrdinalIgnoreCase) || project.ProjectType.Contains("hackatec", StringComparison.OrdinalIgnoreCase)));
                bool hasDoc = false;

                if (isAccreditation)
                {
                    hasDoc = true;
                }
                else
                {
                    hasDoc = await _context.Documents.AnyAsync(d =>
                        d.ProjectId == project.Id &&
                        d.IsActive &&
                        (d.DocumentType == DocumentType.CartaAceptacion || d.DocumentType == DocumentType.CartaAprobacion));
                }

                if (!hasDoc)
                {
                    continue; // Omitir: requiere carta de aceptación oficial
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

                project.AdvisorId = advisor.Id;
                project.UpdatedAt = DateTime.UtcNow;
                project.UpdatedBy = _currentUser.UserId;
                await _projectRepository.UpdateAsync(project);

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

        var requiredColumns = new List<string>
        {
            "Matricula", "Apellidos", "Nombre", "Sexo", "Carrera", "Semestre", "Email"
        };
        var optionalColumns = new List<string>
        {
            "Actividades Complementarias", "Servicio Social", "Especiales"
        };

        using var stream = file.OpenReadStream();
        var (isValid, errorMessage, rows) = ExcelHelper.ParseExcelFile(stream, requiredColumns, optionalColumns);

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
            string gender = s switch
            {
                "M" or "MASCULINO" or "MASC" or "H" or "HOMBRE" => "Masculino",
                "F" or "FEMENINO" or "FEM" or "MUJER" => "Femenino",
                _ => StringSanitizer.SanitizeText(sexoStr)
            };
            int? periodId = parsedSem;

            // Extraer y procesar las 3 columnas de requisitos para bloqueo
            var compColPresent = HasRequirementColumnInFile(row, "Actividades Complementarias", "ActividadesComplementarias", "Complementarias", "ActComplementarias", "HasComplementaryActivities");
            var socColPresent = HasRequirementColumnInFile(row, "Servicio Social", "ServicioSocial", "HasSocialService");
            var specColPresent = HasRequirementColumnInFile(row, "Especiales", "Requisitos Especiales", "RequisitosEspeciales", "Cursos Especiales", "HasSpecialRequirements");

            var compRaw = GetRequirementColumnValue(row, "Actividades Complementarias", "ActividadesComplementarias", "Complementarias", "ActComplementarias", "HasComplementaryActivities");
            var socRaw = GetRequirementColumnValue(row, "Servicio Social", "ServicioSocial", "HasSocialService");
            var specRaw = GetRequirementColumnValue(row, "Especiales", "Requisitos Especiales", "RequisitosEspeciales", "Cursos Especiales", "HasSpecialRequirements");

            // Validar y parsear las 3 columnas de requisitos (1=Sí, 0=No)
            var (isCompValid, reqComp) = TryParseRequirement(compRaw, false);
            if (!isCompValid)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El valor '{compRaw}' en Actividades Complementarias no es válido. Debe ser 1 (Sí) o 0 (No).");
                continue;
            }

            var (isSocValid, reqSoc) = TryParseRequirement(socRaw, false);
            if (!isSocValid)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El valor '{socRaw}' en Servicio Social no es válido. Debe ser 1 (Sí) o 0 (No).");
                continue;
            }

            var (isSpecValid, reqSpec) = TryParseRequirement(specRaw, false);
            if (!isSpecValid)
            {
                result.ErrorCount++;
                result.Errors.Add($"Fila {rowNum}: El valor '{specRaw}' en Especiales no es válido. Debe ser 1 (Sí) o 0 (No).");
                continue;
            }

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

                if (compColPresent) existingStudent.HasComplementaryActivities = reqComp;
                if (socColPresent) existingStudent.HasSocialService = reqSoc;
                if (specColPresent) existingStudent.HasSpecialRequirements = reqSpec;

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
                await CheckAndApplyAutoBlockAsync(existingStudent);

                result.UpdatedCount++;
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
                HasComplementaryActivities = compColPresent ? reqComp : false,
                HasSocialService = socColPresent ? reqSoc : false,
                HasSpecialRequirements = specColPresent ? reqSpec : false,
                IsActive = true,
                CreatedBy = _currentUser.UserId,
                User = createdUser
            };

            await _studentRepository.AddAsync(newStudent);
            await CheckAndApplyAutoBlockAsync(newStudent);

            result.CreatedCount++;
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

    private static (bool IsValid, bool Value) TryParseRequirement(string? rawValue, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return (true, defaultValue);
        var clean = rawValue.Trim().ToLowerInvariant();
        clean = clean.Replace("í", "i").Replace("á", "a").Replace("é", "e").Replace("ó", "o").Replace("ú", "u");
        return clean switch
        {
            "1" or "1.0" or "si" or "s" or "true" or "verdadero" or "x" or "ok" or "cumple" or "liberado" or "aprobado" or "acreditado" => (true, true),
            "0" or "0.0" or "no" or "n" or "false" or "falso" => (true, false),
            _ => (false, false)
        };
    }

    private static string? GetRequirementColumnValue(Dictionary<string, string> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            var normKey = ExcelHelper.NormalizeColumnName(key);
            foreach (var kvp in row)
            {
                if (ExcelHelper.NormalizeColumnName(kvp.Key) == normKey && !string.IsNullOrWhiteSpace(kvp.Value))
                {
                    return kvp.Value;
                }
            }
        }
        return null;
    }

    private static bool HasRequirementColumnInFile(Dictionary<string, string> row, params string[] possibleKeys)
    {
        foreach (var key in possibleKeys)
        {
            var normKey = ExcelHelper.NormalizeColumnName(key);
            foreach (var kvp in row)
            {
                if (ExcelHelper.NormalizeColumnName(kvp.Key) == normKey)
                {
                    return true;
                }
            }
        }
        return false;
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

    public async Task<Result<bool>> BlockStudentAsync(long studentId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result<bool>.Failure("La razón del bloqueo es obligatoria.", 400);

        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            return Result<bool>.Failure("Estudiante no encontrado", 404);

        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result<bool>.Failure("Solo administradores pueden bloquear manualmente.", 403);

        var existingBlock = await _studentRepository.GetActiveBlockAsync(studentId);
        if (existingBlock != null)
            return Result<bool>.Failure("El estudiante ya tiene un bloqueo activo.", 400);

        var block = new StudentBlock
        {
            StudentId = studentId,
            Reason = $"[Bloqueo manual] {reason}",
            BlockedBy = _currentUser.UserId,
            BlockedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _studentRepository.AddBlockAsync(block);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> UnblockStudentAsync(long studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            return Result<bool>.Failure("Estudiante no encontrado", 404);

        if (!_currentUser.IsInRole(UserRole.Admin))
            return Result<bool>.Failure("Solo administradores pueden desbloquear.", 403);

        var activeBlock = await _studentRepository.GetActiveBlockAsync(studentId);
        if (activeBlock == null)
            return Result<bool>.Failure("El estudiante no tiene un bloqueo activo.", 400);

        activeBlock.IsActive = false;
        activeBlock.UnblockedAt = DateTime.UtcNow;
        activeBlock.UnblockedBy = _currentUser.UserId;

        await _studentRepository.UpdateBlockAsync(activeBlock);
        return Result<bool>.Success(true);
    }

    public async Task<Result<List<StudentBlock>>> GetBlockHistoryAsync(long studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student is null)
            return Result<List<StudentBlock>>.Failure("Estudiante no encontrado", 404);

        var history = await _studentRepository.GetBlockHistoryAsync(studentId);
        return Result<List<StudentBlock>>.Success(history);
    }

    public async Task<Result<StudentDeadlineInfoDto>> GetStudentDocumentDeadlinesAsync(long id)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<StudentDeadlineInfoDto>.Failure("Estudiante no encontrado", 404);

        return await BuildStudentDeadlineInfoAsync(student);
    }

    public async Task<Result<StudentDeadlineInfoDto>> GetMyDocumentDeadlinesAsync(long userId)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

        if (student is null)
            return Result<StudentDeadlineInfoDto>.Failure("Perfil de estudiante no encontrado", 404);

        return await BuildStudentDeadlineInfoAsync(student);
    }

    private async Task<Result<StudentDeadlineInfoDto>> BuildStudentDeadlineInfoAsync(Student student)
    {
        var project = await _context.Projects
            .Where(p => p.StudentId == student.Id && p.IsActive)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        var docs = project != null
            ? await _context.Documents
                .Where(d => d.ProjectId == project.Id && d.IsActive)
                .OrderByDescending(d => d.Id)
                .ToListAsync()
            : new List<Document>();

        var f29 = docs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29, StringComparison.OrdinalIgnoreCase));
        var f29v2 = docs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato29V2, StringComparison.OrdinalIgnoreCase));
        var f30 = docs.FirstOrDefault(d => d.DocumentType.Equals(DocumentType.Formato30, StringComparison.OrdinalIgnoreCase));

        bool hasApprovedProject = project != null &&
            (project.Status == ProjectStatus.Approved || project.Status == ProjectStatus.InProgress || project.Status == ProjectStatus.Completed);

        var acceptanceDoc = docs.FirstOrDefault(d =>
            (d.DocumentType.Equals(DocumentType.CartaAceptacion, StringComparison.OrdinalIgnoreCase) ||
             d.DocumentType.Equals(DocumentType.CartaAprobacion, StringComparison.OrdinalIgnoreCase) ||
             d.DocumentType.Equals(DocumentType.ConstanciaAcreditacion, StringComparison.OrdinalIgnoreCase)) &&
            d.IsActive);

        bool hasApprovedAcceptanceLetter = acceptanceDoc != null &&
            string.Equals(acceptanceDoc.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);

        bool isEligibleForDeadlines = hasApprovedProject && hasApprovedAcceptanceLetter;

        bool canUploadSecondPhase = f29 != null && string.Equals(f29.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);

        bool isBlocked = false;
        string? blockedReason = null;

        var f29Deadline = student.Formato29Deadline;
        var f30Deadline = student.Formato30Deadline;
        if (!f29Deadline.HasValue || !f30Deadline.HasValue)
        {
            var global = await _settingService.GetGlobalDocumentDeadlinesAsync();
            if (!f29Deadline.HasValue) f29Deadline = global.Formato29Deadline;
            if (!f30Deadline.HasValue) f30Deadline = global.Formato30Deadline;
        }

        if (isEligibleForDeadlines)
        {
            if (f29Deadline.HasValue && DateTime.UtcNow > f29Deadline.Value)
            {
                if (f29 == null || !string.Equals(f29.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase))
                {
                    isBlocked = true;
                    blockedReason = "La fecha límite del Formato 29 ha vencido y requiere validación de la coordinación.";
                }
            }

            if (f30Deadline.HasValue && DateTime.UtcNow > f30Deadline.Value)
            {
                bool f29v2Ok = f29v2 != null && string.Equals(f29v2.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);
                bool f30Ok = f30 != null && string.Equals(f30.Status, DocumentStatus.Approved, StringComparison.OrdinalIgnoreCase);
                if (!f29v2Ok || !f30Ok)
                {
                    isBlocked = true;
                    blockedReason = "La fecha límite de entrega de los Formatos 29 (segunda entrega) y 30 ha vencido.";
                }
            }
        }

        return Result<StudentDeadlineInfoDto>.Success(new StudentDeadlineInfoDto
        {
            StudentId = student.Id,
            ControlNumber = student.ControlNumber,
            FirstName = student.FirstName,
            LastName = student.LastName,
            LastName2 = student.LastName2 ?? string.Empty,
            FullName = $"{student.FirstName} {student.LastName} {student.LastName2}".Trim().Replace("  ", " "),
            Formato29Deadline = f29Deadline,
            Formato30Deadline = f30Deadline,
            Formato29Status = f29?.Status ?? "not_uploaded",
            Formato29RejectionReason = f29?.RejectionReason,
            Formato29V2Status = f29v2?.Status ?? "not_uploaded",
            Formato29V2RejectionReason = f29v2?.RejectionReason,
            Formato30Status = f30?.Status ?? "not_uploaded",
            Formato30RejectionReason = f30?.RejectionReason,
            CanUploadSecondPhase = canUploadSecondPhase,
            HasApprovedProject = hasApprovedProject,
            HasApprovedAcceptanceLetter = hasApprovedAcceptanceLetter,
            IsEligibleForFormatDeadlines = isEligibleForDeadlines,
            IsDocumentBlocked = isBlocked,
            BlockedReason = blockedReason
        });
    }

    public async Task<Result<bool>> UpdateStudentDocumentDeadlinesAsync(long id, UpdateStudentDocumentDeadlinesDto dto)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        if (student is null)
            return Result<bool>.Failure("Estudiante no encontrado", 404);

        if (dto.Formato29Deadline.HasValue)
        {
            student.Formato29Deadline = dto.Formato29Deadline.Value;
        }

        if (dto.Formato30Deadline.HasValue)
        {
            student.Formato30Deadline = dto.Formato30Deadline.Value;
        }

        await _studentRepository.UpdateAsync(student);
        return Result<bool>.Success(true);
    }

    public async Task<Result<StudentFormatDocumentsDto>> GetStudentFormatDocumentsAsync(long id)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (student == null)
            return Result<StudentFormatDocumentsDto>.Failure("Estudiante no encontrado.", 404);

        var project = await _context.Projects
            .Where(p => p.StudentId == student.Id && p.IsActive)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        var docs = project != null
            ? await _context.Documents
                .Where(d => d.ProjectId == project.Id && d.IsActive)
                .OrderByDescending(d => d.Id)
                .ToListAsync()
            : new List<Document>();

        var docDtos = docs.Select(d => new DocumentResponseDto
        {
            Id = d.Id,
            ProjectId = d.ProjectId,
            DocumentType = d.DocumentType,
            FileName = d.FileName,
            FilePath = d.FilePath,
            FileSize = d.FileSize,
            ContentType = d.ContentType,
            Status = d.Status,
            RejectionReason = d.RejectionReason,
            UploadedAt = d.UploadedAt,
            IsActive = d.IsActive,
            IsVisible = d.IsVisible,
            DisplayOrder = d.DisplayOrder,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            CreatedBy = d.CreatedBy,
            UpdatedBy = d.UpdatedBy,
            DeletedBy = d.DeletedBy,
            DeletedAt = d.DeletedAt
        }).ToList();

        return Result<StudentFormatDocumentsDto>.Success(new StudentFormatDocumentsDto
        {
            StudentId = student.Id,
            ControlNumber = student.ControlNumber,
            FullName = $"{student.FirstName} {student.LastName} {student.LastName2}".Trim().Replace("  ", " "),
            ProjectId = project?.Id,
            ProjectTitle = project?.Title,
            Documents = docDtos
        });
    }

    public async Task<Result<GlobalDocumentDeadlinesDto>> GetGlobalDocumentDeadlinesAsync()
    {
        var global = await _settingService.GetGlobalDocumentDeadlinesAsync();
        return Result<GlobalDocumentDeadlinesDto>.Success(global);
    }

    public async Task<Result<bool>> UpdateGlobalDocumentDeadlinesAsync(GlobalDocumentDeadlinesDto dto, long userId)
    {
        return await _settingService.UpdateGlobalDocumentDeadlinesAsync(dto, userId);
    }

    private static StudentResponseDto MapToResponseDto(Student student)
    {
        var activeBlock = student.Blocks?.FirstOrDefault(b => b.IsActive);

        return new StudentResponseDto
        {
            Id = student.Id,
            UserId = student.UserId,
            ControlNumber = student.ControlNumber,
            FirstName = student.FirstName,
            LastName = student.LastName,
            LastName2 = student.LastName2,
            FullName = $"{student.FirstName} {student.LastName} {student.LastName2}".Trim().Replace("  ", " "),
            Curp = student.Curp,
            Gender = student.Gender,
            CareerId = student.CareerId,
            CareerName = null,
            AdvisorId = student.AdvisorId,
            AdvisorAssignedAt = student.AdvisorAssignedAt,
            AdvisorName = student.Advisor?.FullName,
            AcademicPeriodId = student.AcademicPeriodId,
            Email = student.User?.Email ?? string.Empty,
            Gpa = student.Gpa,
            IsPresentationLetterSent = student.IsPresentationLetterSent,
            PresentationLetterSentAt = student.PresentationLetterSentAt,

            HasComplementaryActivities = student.HasComplementaryActivities,
            HasSocialService = student.HasSocialService,
            HasSpecialRequirements = student.HasSpecialRequirements,

            IsBlocked = activeBlock != null,
            BlockReason = activeBlock?.Reason,

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
