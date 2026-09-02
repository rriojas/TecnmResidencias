using TecNM.Residency.Advisors;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Evaluations;

public class EvaluationService : IEvaluationService
{
    private readonly IEvaluationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IAdvisorRepository _advisorRepository;
    private readonly IProjectRepository _projectRepository;

    public EvaluationService(
        IEvaluationRepository repository,
        ICurrentUserService currentUser,
        IAdvisorRepository advisorRepository,
        IProjectRepository projectRepository)
    {
        _repository = repository;
        _currentUser = currentUser;
        _advisorRepository = advisorRepository;
        _projectRepository = projectRepository;
    }

    private async Task<long?> GetSessionAdvisorIdAsync()
    {
        if (!_currentUser.IsInRole(UserRole.Advisor)) return null;
        var advisor = await _advisorRepository.GetByUserIdAsync(_currentUser.UserId);
        return advisor?.Id;
    }

    private bool IsStaff() =>
        _currentUser.IsInRole(UserRole.Admin) ||
        _currentUser.IsInRole(UserRole.Academic) ||
        _currentUser.IsInRole(UserRole.Vinculacion) ||
        _currentUser.IsInRole(UserRole.Director) ||
        _currentUser.IsInRole(UserRole.DepartmentHead) ||
        _currentUser.IsInRole(UserRole.CareerHead);

    private bool IsAuthorizedForTimeline() =>
        _currentUser.IsInRole(UserRole.Admin) ||
        _currentUser.IsInRole(UserRole.DepartmentHead) ||
        _currentUser.IsInRole(UserRole.CareerHead) ||
        _currentUser.IsInRole(UserRole.Academic) ||
        _currentUser.IsInRole(UserRole.Director);

    public async Task<Result<EvaluationResponseDto>> GradeEvaluationAsync(GradeEvaluationDto dto)
    {
        if (dto.Score < 0 || dto.Score > 100)
            return Result<EvaluationResponseDto>.Failure("La calificación debe estar comprendida entre 0 y 100.");

        var project = await _projectRepository.GetByIdAsync(dto.ProjectId);
        if (project is null)
            return Result<EvaluationResponseDto>.Failure("El anteproyecto especificado no existe.", 404);

        if (_currentUser.IsInRole(UserRole.Advisor) && !IsStaff())
        {
            var advisorId = await GetSessionAdvisorIdAsync();
            if (!advisorId.HasValue || project.AdvisorId != advisorId.Value)
                return Result<EvaluationResponseDto>.Failure("Solo puedes calificar a tus alumnos asignados.", 403);
        }

        var normalizedPeriod = dto.EvaluationPeriod.ToLowerInvariant() switch
        {
            "partial_1" or "parcial_1" or "parcial 1" or "primer reporte parcial" => "partial_1",
            "partial_2" or "parcial_2" or "parcial 2" or "segundo reporte parcial" => "partial_2",
            "final" or "reporte final" => "final",
            _ => dto.EvaluationPeriod.ToLowerInvariant()
        };

        var validPeriods = new[] { "partial_1", "partial_2", "final" };
        if (!validPeriods.Contains(normalizedPeriod))
            return Result<EvaluationResponseDto>.Failure($"Período de evaluación '{dto.EvaluationPeriod}' no es válido.");

        // Atribución inmutable: el evaluador se deriva de la sesión activa.
        long evaluatorId;
        if (_currentUser.IsInRole(UserRole.Advisor))
        {
            var advisorId = await GetSessionAdvisorIdAsync();
            if (!advisorId.HasValue)
                return Result<EvaluationResponseDto>.Failure("No se encontró un perfil de asesor asociado a tu cuenta.", 403);

            evaluatorId = advisorId.Value;
        }
        else if (IsStaff())
        {
            evaluatorId = dto.EvaluatorId;
        }
        else
        {
            return Result<EvaluationResponseDto>.Failure("No tiene permisos para calificar.", 403);
        }

        var eval = new Evaluation
        {
            ProjectId = dto.ProjectId,
            EvaluatorId = evaluatorId,
            EvaluationPeriod = normalizedPeriod,
            Score = dto.Score,
            Feedback = dto.Feedback?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var saved = await _repository.SaveEvaluationAsync(eval);
        return Result<EvaluationResponseDto>.Success(MapEvaluationToDto(saved));
    }

    public async Task<Result<bool>> DeleteEvaluationAsync(long id)
    {
        if (!IsStaff())
            return Result<bool>.Failure("No tiene permisos para eliminar calificaciones.", 403);

        var deleted = await _repository.SoftDeleteEvaluationAsync(id, _currentUser.UserId);
        if (!deleted)
            return Result<bool>.Failure("Calificación no encontrada o ya fue eliminada.", 404);

        return Result<bool>.Success(true);
    }

    public async Task<Result<PaginatedResult<EvaluationResponseDto>>> GetEvaluationsByProjectIdPagedAsync(long projectId, PaginationQuery query)
    {
        var paged = await _repository.GetEvaluationsByProjectIdPagedAsync(projectId, query);
        var dtos = paged.Items.Select(MapEvaluationToDto);
        var result = PaginatedResult<EvaluationResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<EvaluationResponseDto>>.Success(result);
    }

    public async Task<Result<AdvisorySessionResponseDto>> RecordAdvisorySessionAsync(CreateAdvisorySessionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.TopicsCovered))
            return Result<AdvisorySessionResponseDto>.Failure("Debe especificar los temas o avances abordados en la asesoría.");

        var project = await _projectRepository.GetByIdAsync(dto.ProjectId);
        if (project is null)
            return Result<AdvisorySessionResponseDto>.Failure("El anteproyecto especificado no existe.", 404);

        // Atribución inmutable: el asesor se deriva de la sesión activa.
        long advisorId;
        if (_currentUser.IsInRole(UserRole.Advisor))
        {
            var sessionAdvisorId = await GetSessionAdvisorIdAsync();
            if (!sessionAdvisorId.HasValue)
                return Result<AdvisorySessionResponseDto>.Failure("No se encontró un perfil de asesor asociado a tu cuenta.", 403);

            if (project.AdvisorId != sessionAdvisorId.Value && !IsStaff())
                return Result<AdvisorySessionResponseDto>.Failure("Solo puedes registrar asesorías a tus alumnos asignados.", 403);

            advisorId = sessionAdvisorId.Value;
        }
        else if (IsStaff())
        {
            advisorId = dto.AdvisorId;
        }
        else
        {
            return Result<AdvisorySessionResponseDto>.Failure("No tiene permisos para registrar asesorías.", 403);
        }

        var session = new AdvisorySession
        {
            ProjectId = dto.ProjectId,
            AdvisorId = advisorId,
            SessionDate = dto.SessionDate ?? DateTime.UtcNow,
            TopicsCovered = dto.TopicsCovered.Trim(),
            StudentAgreements = dto.StudentAgreements?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId
        };

        var created = await _repository.CreateAdvisorySessionAsync(session);
        return Result<AdvisorySessionResponseDto>.Success(MapSessionToDto(created));
    }

    public async Task<Result<AdvisorySessionResponseDto>> UpdateAdvisorySessionAsync(long id, UpdateAdvisorySessionDto dto)
    {
        if (!IsStaff())
            return Result<AdvisorySessionResponseDto>.Failure("No tiene permisos para editar sesiones de asesoría.", 403);

        if (string.IsNullOrWhiteSpace(dto.TopicsCovered))
            return Result<AdvisorySessionResponseDto>.Failure("Debe especificar los temas o avances abordados en la asesoría.");

        var session = await _repository.GetSessionByIdAsync(id);
        if (session == null || !session.IsActive)
            return Result<AdvisorySessionResponseDto>.Failure("Sesión de asesoría no encontrada.", 404);

        session.AdvisorId = dto.AdvisorId;
        session.SessionDate = dto.SessionDate ?? session.SessionDate;
        session.TopicsCovered = dto.TopicsCovered.Trim();
        session.StudentAgreements = dto.StudentAgreements?.Trim();
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = _currentUser.UserId;

        await _repository.UpdateSessionAsync(session);
        return Result<AdvisorySessionResponseDto>.Success(MapSessionToDto(session));
    }

    public async Task<Result<bool>> DeleteAdvisorySessionAsync(long id)
    {
        if (!IsStaff())
            return Result<bool>.Failure("No tiene permisos para eliminar sesiones de asesoría.", 403);

        var deleted = await _repository.SoftDeleteSessionAsync(id, _currentUser.UserId);
        if (!deleted)
            return Result<bool>.Failure("Sesión de asesoría no encontrada o ya fue eliminada.", 404);

        return Result<bool>.Success(true);
    }

    public async Task<Result<PaginatedResult<AdvisorySessionResponseDto>>> GetAdvisorySessionsByProjectIdPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false)
    {
        var paged = await _repository.GetAdvisorySessionsByProjectIdPagedAsync(projectId, query, includeInactive);
        var dtos = paged.Items.Select(MapSessionToDto);
        var result = PaginatedResult<AdvisorySessionResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<AdvisorySessionResponseDto>>.Success(result);
    }

    public async Task<Result<PaginatedResult<AdvisorySessionResponseDto>>> GetAllAdvisorySessionsPagedAsync(PaginationQuery query, long? projectId, bool includeInactive = false)
    {
        if (!IsStaff())
            return Result<PaginatedResult<AdvisorySessionResponseDto>>.Failure("No tiene permisos para consultar todas las asesorías del sistema.", 403);

        var paged = await _repository.GetAdvisorySessionsPagedAsync(query, projectId, includeInactive);
        var dtos = paged.Items.Select(MapSessionToDto);
        var result = PaginatedResult<AdvisorySessionResponseDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);
        return Result<PaginatedResult<AdvisorySessionResponseDto>>.Success(result);
    }

    public async Task<Result<byte[]>> ExportSessionsPdfAsync(long? projectId, string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        if (!IsStaff())
            return Result<byte[]>.Failure("No tiene permisos para exportar asesorías.", 403);

        var sessions = await _repository.GetAllSessionsForExportAsync(projectId, search, sortBy, sortDir, includeInactive);
        var definition = new PdfTableDefinition
        {
            Title = "Bitácora de Asesorías - TecNM Campus Monclova",
            Headers = new List<string> { "ID", "Proyecto", "Fecha", "Estudiante", "Asesor", "Temas Abordados", "Acuerdos" },
            Rows = sessions.Select(s => new List<string>
            {
                s.Id.ToString(),
                s.Project?.Title ?? $"#{s.ProjectId}",
                s.SessionDate.ToString("dd/MM/yyyy"),
                s.Project?.Student is null ? $"#{s.Project?.StudentId}" : $"{s.Project.Student.FirstName} {s.Project.Student.LastName}".Trim(),
                s.Advisor?.FullName ?? $"#{s.AdvisorId}",
                s.TopicsCovered,
                s.StudentAgreements ?? string.Empty
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    private static EvaluationResponseDto MapEvaluationToDto(Evaluation e)
    {
        var student = e.Project?.Student;
        var studentName = student is null
            ? $"Estudiante #{e.Project?.StudentId}"
            : $"{student.FirstName} {student.LastName}".Trim();

        return new EvaluationResponseDto(
            e.Id,
            e.ProjectId,
            e.EvaluatorId,
            e.EvaluationPeriod,
            e.Score,
            e.Feedback,
            e.CreatedAt,
            e.Project?.Title ?? string.Empty,
            studentName,
            e.IsVisible,
            e.DisplayOrder,
            e.UpdatedAt,
            e.CreatedBy,
            e.UpdatedBy,
            e.DeletedBy,
            e.DeletedAt
        );
    }

    private static AdvisorySessionResponseDto MapSessionToDto(AdvisorySession s)
    {
        var student = s.Project?.Student;
        var studentName = student is null
            ? $"Estudiante #{s.Project?.StudentId}"
            : $"{student.FirstName} {student.LastName}".Trim();

        return new AdvisorySessionResponseDto(
            s.Id,
            s.ProjectId,
            s.AdvisorId,
            s.SessionDate,
            s.TopicsCovered,
            s.StudentAgreements,
            s.CreatedAt,
            s.Project?.Title ?? string.Empty,
            studentName,
            s.Advisor?.FullName ?? string.Empty,
            s.IsVisible,
            s.DisplayOrder,
            s.UpdatedAt,
            s.CreatedBy,
            s.UpdatedBy,
            s.DeletedBy,
            s.DeletedAt,
            s.ReviewStatus ?? "pending",
            s.ReviewNotes,
            s.ReviewedAt,
            s.ReviewedBy
        );
    }

    public async Task<Result<PaginatedResult<AdvisoryTimelineItemDto>>> GetAdvisoryTimelinePagedAsync(AdvisoryTimelineQuery query)
    {
        if (!IsAuthorizedForTimeline())
            return Result<PaginatedResult<AdvisoryTimelineItemDto>>.Failure("No tiene permisos para consultar el timeline de seguimiento.", 403);

        var paged = await _repository.GetAdvisoryTimelinePagedAsync(query);
        var dtos = paged.Items.Select(MapTimelineItem);
        var result = PaginatedResult<AdvisoryTimelineItemDto>.Create(
            dtos, paged.TotalCount, paged.PageNumber, paged.PageSize);

        return Result<PaginatedResult<AdvisoryTimelineItemDto>>.Success(result);
    }

    public async Task<Result<AdvisoryTimelineSummaryDto>> GetAdvisorsHealthStatusAsync(long? careerId)
    {
        if (!IsAuthorizedForTimeline())
            return Result<AdvisoryTimelineSummaryDto>.Failure("No tiene permisos para consultar el estado de salud de los asesores.", 403);

        var summary = await _repository.GetAdvisorsHealthStatusAsync(careerId);
        return Result<AdvisoryTimelineSummaryDto>.Success(summary);
    }

    public async Task<Result<bool>> SaveSupervisionNoteAsync(long id, SaveSupervisionNoteDto dto)
    {
        if (!_currentUser.IsInRole(UserRole.Admin) && !_currentUser.IsInRole(UserRole.DepartmentHead) && !_currentUser.IsInRole(UserRole.CareerHead))
            return Result<bool>.Failure("Solo el Jefe de Carrera o personal directivo pueden registrar notas de supervisión.", 403);

        var ok = await _repository.SaveSupervisionNoteAsync(id, dto.Notes, _currentUser.UserId);
        if (!ok)
            return Result<bool>.Failure("No se encontró la asesoría o no pertenece a su carrera asignada.", 404);

        return Result<bool>.Success(true);
    }

    public async Task<Result<byte[]>> ExportTimelinePdfAsync(AdvisoryTimelineQuery query)
    {
        if (!IsAuthorizedForTimeline())
            return Result<byte[]>.Failure("No tiene permisos para exportar el timeline de seguimiento.", 403);

        query.PageSize = 1000;
        var paged = await _repository.GetAdvisoryTimelinePagedAsync(query);

        var definition = new PdfTableDefinition
        {
            Title = "Timeline de Seguimiento de Asesorías - TecNM Campus Monclova",
            Headers = new List<string> { "ID", "Fecha", "Asesor", "Estudiante", "Proyecto", "Temas Abordados", "Nota Jefatura" },
            Rows = paged.Items.Select(s => new List<string>
            {
                s.Id.ToString(),
                s.SessionDate.ToString("dd/MM/yyyy"),
                s.Advisor?.FullName ?? $"#{s.AdvisorId}",
                s.Project?.Student != null ? $"{s.Project.Student.FirstName} {s.Project.Student.LastName}".Trim() : $"#{s.Project?.StudentId}",
                s.Project?.Title ?? $"#{s.ProjectId}",
                s.TopicsCovered,
                !string.IsNullOrWhiteSpace(s.ReviewNotes) ? s.ReviewNotes : "Sin observaciones"
            }).ToList()
        };

        return Result<byte[]>.Success(PdfExportService.GenerateTablePdf(definition));
    }

    private static AdvisoryTimelineItemDto MapTimelineItem(AdvisorySession s)
    {
        var student = s.Project?.Student;
        var studentName = student is null
            ? $"Estudiante #{s.Project?.StudentId}"
            : $"{student.FirstName} {student.LastName}".Trim();

        var careerId = student?.CareerId ?? 0;
        var careerName = careerId switch
        {
            1 => "Ing. Informática",
            2 => "Ing. Industrial",
            3 => "Ing. Mecatrónica",
            4 => "Ing. en Sistemas Computacionales",
            5 => "Ing. Electrónica",
            6 => "Ing. en Gestión Empresarial",
            _ => "Ingeniería"
        };

        return new AdvisoryTimelineItemDto
        {
            Id = s.Id,
            ProjectId = s.ProjectId,
            ProjectTitle = s.Project?.Title ?? $"Proyecto #{s.ProjectId}",
            ProjectStatus = s.Project?.Status.ToString() ?? string.Empty,
            AdvisorId = s.AdvisorId,
            AdvisorName = s.Advisor?.FullName ?? $"Asesor #{s.AdvisorId}",
            AdvisorTitle = s.Advisor?.Title,
            AdvisorEmail = s.Advisor?.User?.Email,
            StudentId = student?.Id ?? 0,
            StudentName = studentName,
            StudentControlNumber = student?.ControlNumber ?? string.Empty,
            CareerId = careerId,
            CareerName = careerName,
            SessionDate = s.SessionDate,
            TopicsCovered = s.TopicsCovered,
            StudentAgreements = s.StudentAgreements,
            SupervisionNotes = s.ReviewNotes,
            SupervisedAt = s.ReviewedAt,
            SupervisedBy = s.ReviewedBy,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            IsActive = s.IsActive
        };
    }
}
