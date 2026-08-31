using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Companies;
using TecNM.Residency.Students;

namespace TecNM.Residency.Projects;

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IStudentRepository _studentRepository;
    private readonly IAdvisorRepository _advisorRepository;
    private readonly ICompanyRepository _companyRepository;

    public ProjectService(
        IProjectRepository repository,
        ICurrentUserService currentUser,
        IStudentRepository studentRepository,
        IAdvisorRepository advisorRepository,
        ICompanyRepository companyRepository)
    {
        _repository = repository;
        _currentUser = currentUser;
        _studentRepository = studentRepository;
        _advisorRepository = advisorRepository;
        _companyRepository = companyRepository;
    }

    private bool IsStaff() =>
        _currentUser.IsInRole(UserRole.Admin) ||
        _currentUser.IsInRole(UserRole.Academic) ||
        _currentUser.IsInRole(UserRole.Vinculacion) ||
        _currentUser.IsInRole(UserRole.Director) ||
        _currentUser.IsInRole(UserRole.DepartmentHead);

    private async Task<Student?> GetSessionStudentAsync()
    {
        if (!_currentUser.IsInRole(UserRole.Student)) return null;
        return await _studentRepository.GetByUserIdAsync(_currentUser.UserId);
    }

    private async Task<Advisor?> GetSessionAdvisorAsync()
    {
        if (!_currentUser.IsInRole(UserRole.Advisor)) return null;
        return await _advisorRepository.GetByUserIdAsync(_currentUser.UserId);
    }

    public async Task<Result<ProjectResponseDto>> CreateProjectAsync(CreateProjectDto dto)
    {
        if (dto is null)
            return Result<ProjectResponseDto>.Failure("El anteproyecto no puede ser nulo.");

        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ProjectResponseDto>.Failure("El título del anteproyecto es obligatorio.");

        if (string.IsNullOrWhiteSpace(dto.ProblemStatement))
            return Result<ProjectResponseDto>.Failure("El planteamiento del problema es obligatorio.");

        if (string.IsNullOrWhiteSpace(dto.Justification))
            return Result<ProjectResponseDto>.Failure("La justificación es obligatoria.");

        if (string.IsNullOrWhiteSpace(dto.GeneralObjective))
            return Result<ProjectResponseDto>.Failure("El objetivo general es obligatorio.");

        if (dto.CompanyId <= 0)
            return Result<ProjectResponseDto>.Failure("Debe seleccionar una empresa receptora vinculada obligatoriamente al anteproyecto.");

        var company = await _companyRepository.GetByIdAsync(dto.CompanyId);
        if (company is null)
            return Result<ProjectResponseDto>.Failure("La empresa receptora seleccionada no existe o no está registrada.");

        // Resolver la atribución del anteproyecto desde la sesión activa.
        long studentId;
        long? advisorId = null;
        Student? targetStudent = null;

        if (_currentUser.IsInRole(UserRole.Student))
        {
            targetStudent = await _studentRepository.GetByUserIdAsync(_currentUser.UserId);
            if (targetStudent is null)
                return Result<ProjectResponseDto>.Failure("No se encontró un perfil de estudiante asociado a tu cuenta.", 403);

            studentId = targetStudent.Id;
        }
        else if (_currentUser.IsInRole(UserRole.Admin))
        {
            if (!dto.StudentId.HasValue || dto.StudentId.Value <= 0)
                return Result<ProjectResponseDto>.Failure("Los administradores no pueden registrar anteproyectos a nombre propio. Debe seleccionar obligatoriamente al estudiante destinatario.");

            targetStudent = await _studentRepository.GetByIdAsync(dto.StudentId.Value);
            if (targetStudent is null)
                return Result<ProjectResponseDto>.Failure("El estudiante especificado no existe o no está registrado en el sistema.", 404);

            studentId = targetStudent.Id;
        }
        else
        {
            return Result<ProjectResponseDto>.Failure("No tiene permisos para registrar anteproyectos. Esta acción solo la realizan los estudiantes o el Administrador asignado a un residente.", 403);
        }

        // Regla de negocio: El anteproyecto se puede crear sin un asesor asignado previamente.
        long? targetAdvisorId = dto.AdvisorId > 0 ? dto.AdvisorId : targetStudent?.AdvisorId;
        if (targetAdvisorId.HasValue && targetAdvisorId.Value > 0)
        {
            var assignedAdvisor = await _advisorRepository.GetByIdAsync(targetAdvisorId.Value);
            if (assignedAdvisor is null)
                return Result<ProjectResponseDto>.Failure("El asesor asignado especificado no existe o no está registrado.", 404);

            advisorId = assignedAdvisor.Id;
        }

        // Regla de negocio: un estudiante solo puede tener un proyecto vigente a la vez.
        var activeProject = await _repository.GetActiveByStudentIdAsync(studentId);
        if (activeProject is not null)
            return Result<ProjectResponseDto>.Failure(
                $"Ya cuentas con un anteproyecto vigente (#{activeProject.Id} — {activeProject.Title}). No puedes registrar una nueva solicitud hasta que sea dictaminado o cancelado.",
                409);

        var project = new Project
        {
            StudentId = studentId,
            AdvisorId = advisorId,
            CompanyId = dto.CompanyId,
            Title = dto.Title.Trim(),
            ProjectType = dto.ProjectType?.Trim(),
            ProblemStatement = dto.ProblemStatement.Trim(),
            Justification = dto.Justification.Trim(),
            GeneralObjective = dto.GeneralObjective.Trim(),
            Status = ProjectStatus.Draft,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        if (dto.SpecificObjectives != null && dto.SpecificObjectives.Count > 0)
        {
            int number = 1;
            foreach (var objText in dto.SpecificObjectives)
            {
                if (!string.IsNullOrWhiteSpace(objText))
                {
                    project.Objectives.Add(new ProjectObjective
                    {
                        ObjectiveNumber = number++,
                        Description = objText.Trim(),
                        Status = "pending",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        var created = await _repository.CreateAsync(project);
        return Result<ProjectResponseDto>.Success(MapToDto(created));
    }

    public async Task<Result<ProjectResponseDto>> UpdateProjectAsync(long id, UpdateProjectDto dto)
    {
        var access = await CanAccessProjectAsync(id);
        if (!access.IsSuccess)
            return Result<ProjectResponseDto>.Failure(access.ErrorMessage!, access.StatusCode ?? 403);

        if (!IsStaff() && !_currentUser.IsInRole(UserRole.Student))
            return Result<ProjectResponseDto>.Failure("No tiene permisos para editar este anteproyecto.", 403);

        var project = await _repository.GetByIdAsync(id);
        if (project is null)
            return Result<ProjectResponseDto>.Failure("Anteproyecto no encontrado.", 404);

        // Los anteproyectos aprobados, en progreso, completados o cancelados no se pueden modificar.
        if (project.Status is ProjectStatus.Approved or ProjectStatus.InProgress or ProjectStatus.Completed or ProjectStatus.Cancelled)
            return Result<ProjectResponseDto>.Failure("No se pueden modificar anteproyectos aprobados, en progreso, completados o cancelados.", 400);

        // Los estudiantes solo pueden editar mientras el anteproyecto es borrador o fue devuelto con correcciones.
        if (!IsStaff() && project.Status is not (ProjectStatus.Draft or ProjectStatus.Rejected))
            return Result<ProjectResponseDto>.Failure("Solo puedes editar anteproyectos en estado de borrador. Envíalo a revisión para que la División lo dictamine.", 403);

        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<ProjectResponseDto>.Failure("El título del proyecto es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.ProblemStatement))
            return Result<ProjectResponseDto>.Failure("El planteamiento del problema es obligatorio.");
        if (string.IsNullOrWhiteSpace(dto.Justification))
            return Result<ProjectResponseDto>.Failure("La justificación es obligatoria.");
        if (string.IsNullOrWhiteSpace(dto.GeneralObjective))
            return Result<ProjectResponseDto>.Failure("El objetivo general es obligatorio.");

        project.Title = dto.Title.Trim();
        project.ProjectType = dto.ProjectType?.Trim();
        project.ProblemStatement = dto.ProblemStatement.Trim();
        project.Justification = dto.Justification.Trim();
        project.GeneralObjective = dto.GeneralObjective.Trim();
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;

        project.Objectives.Clear();
        if (dto.SpecificObjectives != null && dto.SpecificObjectives.Count > 0)
        {
            int number = 1;
            foreach (var objText in dto.SpecificObjectives)
            {
                if (!string.IsNullOrWhiteSpace(objText))
                {
                    project.Objectives.Add(new ProjectObjective
                    {
                        ObjectiveNumber = number++,
                        Description = objText.Trim(),
                        Status = "pending",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _repository.UpdateWithObjectivesAsync(project);
        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<ProjectResponseDto>> SendToReviewAsync(long id)
    {
        var access = await CanAccessProjectAsync(id);
        if (!access.IsSuccess)
            return Result<ProjectResponseDto>.Failure(access.ErrorMessage!, access.StatusCode ?? 403);

        if (!IsStaff() && !_currentUser.IsInRole(UserRole.Student))
            return Result<ProjectResponseDto>.Failure("No tiene permisos para enviar este anteproyecto a revisión.", 403);

        var project = await _repository.GetByIdAsync(id);
        if (project is null)
            return Result<ProjectResponseDto>.Failure("Anteproyecto no encontrado.", 404);

        if (project.Status is not (ProjectStatus.Draft or ProjectStatus.Rejected))
            return Result<ProjectResponseDto>.Failure("Solo se pueden enviar a revisión anteproyectos en estado de borrador o devueltos con correcciones.", 400);

        project.Status = ProjectStatus.Pending;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;

        await _repository.UpdateAsync(project);
        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<ProjectResponseDto>> GetProjectByIdAsync(long id)
    {
        var access = await CanAccessProjectAsync(id);
        if (!access.IsSuccess)
            return Result<ProjectResponseDto>.Failure(access.ErrorMessage!, access.StatusCode ?? 403);

        var project = await _repository.GetByIdAsync(id);
        if (project == null)
            return Result<ProjectResponseDto>.Failure("Anteproyecto no encontrado.", 404);

        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<ProjectResponseDto>> GetProjectByStudentIdAsync(long studentId)
    {
        var project = await _repository.GetByStudentIdAsync(studentId);
        if (project == null)
            return Result<ProjectResponseDto>.Failure("No se encontró ningún anteproyecto para este estudiante.", 404);

        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<ProjectResponseDto>> GetMyCurrentProjectAsync()
    {
        var student = await GetSessionStudentAsync();
        if (student is null)
            return Result<ProjectResponseDto>.Failure("Este recurso es exclusivo para estudiantes.", 403);

        var project = await _repository.GetPrimaryProjectByStudentIdAsync(student.Id);
        if (project == null)
            return Result<ProjectResponseDto>.Failure("No se encontró ningún anteproyecto o residencia registrada para tu cuenta.", 404);

        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<PaginatedResult<ProjectResponseDto>>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        // Vista Estudiante: únicamente sus registros.
        if (_currentUser.IsInRole(UserRole.Student))
            return await GetMyProjectsPagedAsync(query);

        // Vista Administradores / Jefes de División / Vinculación / Dirección: ven todos los anteproyectos.
        if (_currentUser.IsInRole(UserRole.Admin) ||
            _currentUser.IsInRole(UserRole.Vinculacion) ||
            _currentUser.IsInRole(UserRole.DepartmentHead) ||
            _currentUser.IsInRole(UserRole.Director))
        {
            var pagedAll = await _repository.GetPagedAsync(query, status, includeInactive);
            return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(pagedAll));
        }

        // Vista Asesor exclusivo: únicamente los anteproyectos bajo su asesoría.
        if (_currentUser.IsInRole(UserRole.Advisor))
            return await GetAdvisorProjectsPagedAsync(query);

        // Fallback general: todos los registros.
        var paged = await _repository.GetPagedAsync(query, status, includeInactive);
        return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(paged));
    }

    public async Task<Result<byte[]>> ExportPdfAsync(string? status, string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        if (!IsStaff())
            return Result<byte[]>.Failure("No tiene permisos para exportar anteproyectos.", 403);

        var projects = await _repository.GetAllForExportAsync(status, search, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Anteproyectos de Residencia Profesional - TecNM Campus Monclova",
            Headers = new List<string> { "ID", "Título", "Estudiante", "No. Control", "Asesor", "Estado", "Creado el" },
            Rows = projects.Select(p => new List<string>
            {
                p.Id.ToString(),
                p.Title,
                p.Student is null ? $"#{p.StudentId}" : $"{p.Student.FirstName} {p.Student.LastName}".Trim(),
                p.Student?.ControlNumber ?? string.Empty,
                p.Advisor?.FullName ?? string.Empty,
                p.Status.ToString(),
                p.CreatedAt.ToString("dd/MM/yyyy")
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    public async Task<Result<byte[]>> GetProjectPdfAsync(long id)
    {
        var access = await CanAccessProjectAsync(id);
        if (!access.IsSuccess)
            return Result<byte[]>.Failure(access.ErrorMessage!, access.StatusCode ?? 403);

        var project = await _repository.GetByIdAsync(id);
        if (project is null)
            return Result<byte[]>.Failure("Anteproyecto no encontrado.", 404);

        // Regla de negocio: solo se puede imprimir un anteproyecto aprobado o verificado para su realización.
        if (project.Status is not (ProjectStatus.Approved or ProjectStatus.InProgress))
            return Result<byte[]>.Failure("El anteproyecto no está aprobado o verificado para su realización; por lo tanto, aún no puede imprimirse.", 409);

        var studentName = project.Student is null
            ? string.Empty
            : $"{project.Student.FirstName} {project.Student.LastName}".Trim();

        var pdfData = new ProjectPdfData(
            studentName,
            project.Company?.Name ?? string.Empty,
            project.Company?.Rfc ?? string.Empty,
            project.Company?.Sector,
            project.Company?.Address,
            project.Company?.ContactName ?? string.Empty,
            project.Company?.ContactEmail ?? string.Empty,
            project.Company?.ContactPhone,
            project.Advisor?.FullName ?? string.Empty,
            project.Title,
            project.ProjectType,
            project.ProblemStatement,
            project.Justification,
            project.GeneralObjective,
            project.Objectives
                .Where(o => o.IsActive)
                .OrderBy(o => o.ObjectiveNumber)
                .Select(o => o.Description)
                .ToList()
        );

        return Result<byte[]>.Success(ProjectPdfService.GenerateProjectPdf(pdfData));
    }

    public async Task<Result<PaginatedResult<ProjectResponseDto>>> GetProjectsByStudentIdPagedAsync(long studentId, PaginationQuery query)
    {
        var paged = await _repository.GetPagedByStudentIdAsync(studentId, query);
        return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(paged));
    }

    public async Task<Result<PaginatedResult<ProjectResponseDto>>> GetMyProjectsPagedAsync(PaginationQuery query, bool includeInactive = false)
    {
        // Vista Administrador/Jefatura: todos los registros (evita filtrar por usuario actual).
        if (IsStaff())
        {
            var all = await _repository.GetPagedAsync(query, "all", includeInactive);
            return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(all));
        }

        var student = await GetSessionStudentAsync();
        if (student is null)
            return Result<PaginatedResult<ProjectResponseDto>>.Failure("No se encontró un perfil de estudiante asociado a tu cuenta.", 404);

        var paged = await _repository.GetPagedByStudentIdAsync(student.Id, query, includeInactive);
        return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(paged));
    }

    public async Task<Result<PaginatedResult<ProjectResponseDto>>> GetAdvisorProjectsPagedAsync(PaginationQuery query)
    {
        if (IsStaff())
        {
            var all = await _repository.GetPagedAsync(query, "all");
            return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(all));
        }

        var advisor = await GetSessionAdvisorAsync();
        if (advisor is null)
            return Result<PaginatedResult<ProjectResponseDto>>.Failure("No se encontró un perfil de asesor asociado a tu cuenta.", 404);

        var paged = await _repository.GetPagedByAdvisorIdAsync(advisor.Id, query);
        return Result<PaginatedResult<ProjectResponseDto>>.Success(MapPaged(paged));
    }

    public async Task<Result<List<ProjectOptionDto>>> GetOptionsAsync()
    {
        long? studentId = null;
        long? advisorId = null;

        if (_currentUser.IsInRole(UserRole.Student))
        {
            var student = await GetSessionStudentAsync();
            if (student is null)
                return Result<List<ProjectOptionDto>>.Failure("No se encontró un perfil de estudiante asociado a tu cuenta.", 404);
            studentId = student.Id;
        }
        else if (_currentUser.IsInRole(UserRole.Advisor))
        {
            var advisor = await GetSessionAdvisorAsync();
            if (advisor is null)
                return Result<List<ProjectOptionDto>>.Failure("No se encontró un perfil de asesor asociado a tu cuenta.", 404);
            advisorId = advisor.Id;
        }

        var projects = await _repository.GetOptionsAsync(studentId, advisorId);
        var dtos = projects.Select(p => new ProjectOptionDto
        {
            Id = p.Id,
            Title = p.Title
        }).ToList();

        return Result<List<ProjectOptionDto>>.Success(dtos);
    }

    public async Task<Result<bool>> CanAccessProjectAsync(long projectId)
    {
        var project = await _repository.GetByIdAsync(projectId);
        if (project is null)
            return Result<bool>.Failure("Anteproyecto no encontrado.", 404);

        if (IsStaff())
            return Result<bool>.Success(true);

        if (_currentUser.IsInRole(UserRole.Student))
        {
            var student = await _studentRepository.GetByUserIdAsync(_currentUser.UserId);
            if ((student is not null && project.StudentId == student.Id) || (project.Student is not null && project.Student.UserId == _currentUser.UserId))
                return Result<bool>.Success(true);

            return Result<bool>.Failure("No tiene acceso a este anteproyecto.", 403);
        }

        if (_currentUser.IsInRole(UserRole.Advisor))
        {
            var advisor = await _advisorRepository.GetByUserIdAsync(_currentUser.UserId);
            if ((advisor is not null && project.AdvisorId == advisor.Id) || (project.Advisor is not null && project.Advisor.UserId == _currentUser.UserId))
                return Result<bool>.Success(true);

            return Result<bool>.Failure("No tiene acceso a este anteproyecto.", 403);
        }

        return Result<bool>.Failure("No autorizado.", 403);
    }

    public async Task<Result<ProjectResponseDto>> UpdateStatusAsync(long id, UpdateProjectStatusDto dto)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null)
            return Result<ProjectResponseDto>.Failure("Anteproyecto no encontrado.", 404);

        if (!IsStaff())
        {
            var advisor = await GetSessionAdvisorAsync();
            if (advisor is null || project.AdvisorId != advisor.Id)
                return Result<ProjectResponseDto>.Failure("No tiene permisos para dictaminar este anteproyecto.", 403);
        }

        if (project.Status == ProjectStatus.Completed)
            return Result<ProjectResponseDto>.Failure("El proyecto ya se encuentra completado y no se puede modificar su dictamen.", 400);

        if (project.Status == ProjectStatus.Cancelled)
            return Result<ProjectResponseDto>.Failure("El proyecto está cancelado y no se puede modificar su dictamen.", 400);

        // La División no puede dictaminar un anteproyecto que sigue en borrador (no enviado a revisión).
        if (project.Status == ProjectStatus.Draft)
            return Result<ProjectResponseDto>.Failure("El anteproyecto está en borrador. Debe enviarse a revisión antes de ser dictaminado.", 400);

        if (!Enum.TryParse<ProjectStatus>(dto.Status, true, out var newStatus))
        {
            // Try mapping legacy, snake_case or Spanish strings
            newStatus = (dto.Status ?? "").ToLowerInvariant() switch
            {
                "approved" or "aprobado" => ProjectStatus.Approved,
                "rejected" or "rechazado" => ProjectStatus.Rejected,
                "under_review" or "underreview" or "en revision" or "en revisión" => ProjectStatus.UnderReview,
                "draft" or "borrador" => ProjectStatus.Draft,
                "pending" or "pendiente" => ProjectStatus.Pending,
                "proposed" or "propuesto" => ProjectStatus.Proposed,
                "in_progress" or "inprogress" or "en progreso" => ProjectStatus.InProgress,
                "completed" or "completado" => ProjectStatus.Completed,
                "cancelled" or "cancelado" => ProjectStatus.Cancelled,
                _ => (ProjectStatus)(-1)
            };

            if ((int)newStatus == -1)
                return Result<ProjectResponseDto>.Failure($"Estado '{dto.Status}' no es válido.");
        }

        if (project.Status == ProjectStatus.Approved && newStatus == ProjectStatus.Rejected)
        {
            return Result<ProjectResponseDto>.Failure("No se pueden solicitar correcciones a un anteproyecto que ya fue Aprobado.", 400);
        }

        if (project.Status == ProjectStatus.InProgress && newStatus is ProjectStatus.Draft or ProjectStatus.Pending or ProjectStatus.UnderReview or ProjectStatus.Rejected or ProjectStatus.Approved)
        {
            return Result<ProjectResponseDto>.Failure("Un proyecto en progreso no puede ser dictaminado ni retornado a revisión.", 400);
        }

        if (newStatus == ProjectStatus.Approved && !_currentUser.IsInRole(UserRole.Admin) && !_currentUser.IsInRole(UserRole.Vinculacion))
        {
            return Result<ProjectResponseDto>.Failure("Solo el Administrador y el personal de Vinculación tienen permiso para aprobar anteproyectos.", 403);
        }

        project.Status = newStatus;
        if (dto.Comments != null)
        {
            project.ReviewComments = dto.Comments.Trim();
        }
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;

        await _repository.UpdateAsync(project);
        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<ProjectResponseDto>> CancelProjectAsync(long id)
    {
        var access = await CanAccessProjectAsync(id);
        if (!access.IsSuccess)
            return Result<ProjectResponseDto>.Failure(access.ErrorMessage!, access.StatusCode ?? 403);

        if (!IsStaff() && !_currentUser.IsInRole(UserRole.Student))
            return Result<ProjectResponseDto>.Failure("No tiene permisos para cancelar este anteproyecto.", 403);

        var project = await _repository.GetByIdAsync(id);
        if (project is null)
            return Result<ProjectResponseDto>.Failure("Anteproyecto no encontrado.", 404);

        // Regla institucional: Los estudiantes solo pueden cancelar su solicitud antes de que sea aprobada (Borrador, En Revisión o Rechazado).
        // Una vez aprobado, en curso o concluido, la baja requiere autorización y gestión de la División Académica (Staff).
        if (!IsStaff())
        {
            if (project.Status is ProjectStatus.Approved or ProjectStatus.InProgress or ProjectStatus.Completed)
            {
                return Result<ProjectResponseDto>.Failure(
                    "No puedes cancelar una solicitud de residencia que ya ha sido aprobada o está en curso. Para tramitar una baja, comunícate con la División de Estudios Profesionales.",
                    403
                );
            }

            var student = await _studentRepository.GetByUserIdAsync(_currentUser.UserId);
            if (student is null)
                return Result<ProjectResponseDto>.Failure("No se encontró un perfil de estudiante asociado a tu cuenta.", 404);
        }

        var cancellableStatuses = new[]
        {
            ProjectStatus.Draft,
            ProjectStatus.Pending,
            ProjectStatus.Proposed,
            ProjectStatus.UnderReview,
            ProjectStatus.Approved,
            ProjectStatus.InProgress,
            ProjectStatus.Rejected
        };

        if (!cancellableStatuses.Contains(project.Status))
            return Result<ProjectResponseDto>.Failure("Solo se pueden cancelar solicitudes vigentes o con observaciones.", 400);

        project.Status = ProjectStatus.Cancelled;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;

        await _repository.UpdateAsync(project);
        return Result<ProjectResponseDto>.Success(MapToDto(project));
    }

    public async Task<Result<bool>> SoftDeleteAsync(long id)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null)
            return Result<bool>.Failure("Anteproyecto no encontrado.", 404);

        project.IsActive = false;
        project.DeletedAt = DateTime.UtcNow;
        project.DeletedBy = _currentUser.UserId;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;

        await _repository.UpdateAsync(project);
        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> ActivateAsync(long id)
    {
        var project = await _repository.GetByIdAsync(id);
        if (project == null)
            return Result<bool>.Failure("Anteproyecto no encontrado.", 404);

        project.IsActive = true;
        project.DeletedAt = null;
        project.DeletedBy = null;
        project.UpdatedAt = DateTime.UtcNow;
        project.UpdatedBy = _currentUser.UserId;

        // Al reactivar un anteproyecto cancelado, se revierte a "pendiente de revisión".
        if (project.Status == ProjectStatus.Cancelled)
            project.Status = ProjectStatus.Pending;

        await _repository.UpdateAsync(project);
        return Result<bool>.Success(true);
    }

    private static PaginatedResult<ProjectResponseDto> MapPaged(PaginatedResult<Project> paged)
    {
        var dtos = paged.Items.Select(MapToDto);
        return PaginatedResult<ProjectResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }

    private static ProjectResponseDto MapToDto(Project p)
    {
        var objectives = p.Objectives
            .Where(o => o.IsActive)
            .OrderBy(o => o.ObjectiveNumber)
            .Select(o => new ProjectObjectiveDto(
                o.Id,
                o.ObjectiveNumber,
                o.Description,
                o.Status,
                o.Notes
            ))
            .ToList();

        var studentName = p.Student is null
            ? $"Estudiante #{p.StudentId}"
            : $"{p.Student.FirstName} {p.Student.LastName}".Trim();
        var studentControlNumber = p.Student?.ControlNumber ?? string.Empty;
        var studentEmail = p.Student?.User?.Email ?? string.Empty;
        var advisorName = p.Advisor?.FullName ?? string.Empty;
        var companyName = p.Company?.Name ?? string.Empty;

        var isCompleted = p.Status == ProjectStatus.Completed;
        var isReadOnly = p.Status is ProjectStatus.Completed
            or ProjectStatus.Cancelled
            or ProjectStatus.Rejected
            or ProjectStatus.Pending
            or ProjectStatus.Proposed
            or ProjectStatus.UnderReview;
        var canManageActivities = p.Status is ProjectStatus.Approved or ProjectStatus.InProgress;
        var canUploadDocuments = p.Status is ProjectStatus.Approved or ProjectStatus.InProgress;
        var careerId = p.Student?.CareerId;
        var careerName = careerId switch
        {
            1 => "Ing. Informática",
            2 => "Ing. Industrial",
            3 => "Ing. Mecatrónica",
            4 => "Ing. en Sistemas Computacionales",
            _ => null
        };

        return new ProjectResponseDto(
            p.Id,
            p.StudentId,
            p.AdvisorId,
            p.CompanyId,
            companyName,
            p.Title,
            p.ProjectType,
            p.ProblemStatement,
            p.Justification,
            p.GeneralObjective,
            p.Status.ToString().ToLowerInvariant(),
            p.StartDate,
            p.EndDate,
            p.IsActive,
            p.CreatedAt,
            objectives,
            studentName,
            studentControlNumber,
            studentEmail,
            advisorName,
            p.IsVisible,
            p.DisplayOrder,
            p.UpdatedAt,
            p.CreatedBy,
            p.UpdatedBy,
            p.DeletedBy,
            p.DeletedAt,
            isCompleted,
            isReadOnly,
            canManageActivities,
            canUploadDocuments,
            p.ReviewComments,
            careerId,
            careerName
        );
    }
}
