using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Common.Notifications;
using TecNM.Residency.Documents;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Notifications;

public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly AppDbContext _context;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository repository,
        ICurrentUserService currentUser,
        AppDbContext context,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _context = context;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<Result<NotificationResponseDto>> CreateNotificationAsync(CreateNotificationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return Result<NotificationResponseDto>.Failure("El tema o título es obligatorio.", 400);

        if (string.IsNullOrWhiteSpace(dto.Description))
            return Result<NotificationResponseDto>.Failure("La descripción es obligatoria.", 400);

        if (dto.ExpiresAt <= DateTime.UtcNow)
            return Result<NotificationResponseDto>.Failure("La fecha de expiración debe ser futura.", 400);

        var targetRolesStr = (dto.TargetRoles == null || dto.TargetRoles.Count == 0)
            ? "all"
            : string.Join(",", dto.TargetRoles.Select(r => r.Trim().ToLowerInvariant()));

        var notification = new Notification
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            ExpiresAt = DateTime.SpecifyKind(dto.ExpiresAt, DateTimeKind.Utc),
            TargetRoles = targetRolesStr,
            CareerId = dto.CareerId > 0 ? dto.CareerId : null,
            ResidencyModality = string.IsNullOrWhiteSpace(dto.ResidencyModality) ? null : dto.ResidencyModality.Trim().ToLowerInvariant(),
            TargetUserId = dto.TargetUserId > 0 ? dto.TargetUserId : null,
            SenderId = _currentUser.UserId,
            Type = "manual",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId,
            IsActive = true
        };

        var created = await _repository.CreateAsync(notification);
        var loaded = await _repository.GetByIdAsync(created.Id);

        // Despachar correos en lotes de 300 en segundo plano
        _ = EnqueueBroadcastEmailsAsync(dto, loaded ?? created);

        return Result<NotificationResponseDto>.Success(MapToResponseDto(loaded ?? created));
    }

    public async Task<Result<PaginatedResult<NotificationResponseDto>>> GetHistoryAsync(NotificationHistoryQuery query)
    {
        var paged = await _repository.GetHistoryAsync(query);

        var items = paged.Items.Select(MapToResponseDto).ToList();

        var result = PaginatedResult<NotificationResponseDto>.Create(
            items,
            paged.TotalCount,
            paged.PageNumber,
            paged.PageSize
        );

        return Result<PaginatedResult<NotificationResponseDto>>.Success(result);
    }

    public async Task<Result<List<PendingNotificationDto>>> GetPendingNotificationsAsync()
    {
        var pending = new List<PendingNotificationDto>();
        var userId = _currentUser.UserId;
        if (userId <= 0) return Result<List<PendingNotificationDto>>.Success(pending);

        var userRole = _currentUser.Role.ToString().ToLowerInvariant();
        long? careerId = _currentUser.CareerIds.Count > 0 ? _currentUser.CareerIds.First() : null;
        string? residencyModality = null;

        // Si el usuario es estudiante, obtener su modalidad de residencia del proyecto activo
        if (userRole == "student")
        {
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);

            if (student != null)
            {
                if (student.CareerId > 0) careerId = student.CareerId;

                var project = await _context.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.StudentId == student.Id && p.IsActive);

                if (project != null)
                {
                    residencyModality = project.ProjectType;
                }

                // Generar alertas automáticas de sistema para el estudiante
                await AppendStudentSystemAlertsAsync(student, project, pending);
            }
        }

        // Obtener notificaciones manuales emitidas por personal académico / admin
        var manualList = await _repository.GetPendingForUserAsync(userId, userRole, careerId, residencyModality);

        foreach (var m in manualList)
        {
            pending.Add(new PendingNotificationDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                ExpiresAt = m.ExpiresAt,
                Type = "manual",
                Source = "Aviso Académico",
                ActionUrl = null,
                CreatedAt = m.CreatedAt
            });
        }

        return Result<List<PendingNotificationDto>>.Success(pending.OrderByDescending(p => p.CreatedAt).ToList());
    }

    public async Task<Result<bool>> MarkAsReadAsync(long notificationId)
    {
        if (notificationId <= 0)
            return Result<bool>.Failure("ID de notificación no válido.", 400);

        // Notificaciones del sistema (con IDs sintéticos negativos o virtuales) no requieren persistencia en tabla
        if (notificationId > 0)
        {
            await _repository.MarkAsReadAsync(notificationId, _currentUser.UserId);
        }

        return Result<bool>.Success(true);
    }

    public async Task<Result<bool>> DeleteAsync(long notificationId)
    {
        var success = await _repository.DeleteAsync(notificationId, _currentUser.UserId);
        if (!success)
            return Result<bool>.Failure("Notificación no encontrada o ya eliminada.", 404);

        return Result<bool>.Success(true);
    }

    private async Task AppendStudentSystemAlertsAsync(
        Students.Student student,
        Project? project,
        List<PendingNotificationDto> pending)
    {
        if (project == null) return;

        // IMPORTANTE: Residencias basadas en InnovaTecNM / HackaTec no se meten en estas alertas del sistema
        bool isAccreditation = project.ProjectType is "acreditacion_innovatec" or "acreditacion_hackatec"
            || (!string.IsNullOrWhiteSpace(project.ProjectType) &&
                (project.ProjectType.Contains("innovatec", StringComparison.OrdinalIgnoreCase) ||
                 project.ProjectType.Contains("hackatec", StringComparison.OrdinalIgnoreCase)));

        if (isAccreditation) return;

        // 1. Alerta de Carta de Aceptación pendiente si el proyecto está aprobado pero no tiene carta
        if (project.Status == ProjectStatus.Approved)
        {
            bool hasCarta = await _context.Documents
                .AnyAsync(d => d.ProjectId == project.Id && d.IsActive &&
                    (d.DocumentType == DocumentType.CartaAceptacion || d.DocumentType == DocumentType.CartaAprobacion));

            if (!hasCarta)
            {
                pending.Add(new PendingNotificationDto
                {
                    Id = -101, // ID virtual de sistema
                    Title = "Carta de Aceptación Pendiente",
                    Description = "Tu anteproyecto está aprobado. Sube tu Carta de Aceptación para continuar tu expediente digital.",
                    Type = "system",
                    Source = "Expediente Digital",
                    ActionUrl = "/documents",
                    CreatedAt = project.UpdatedAt
                });
            }
        }

        // 2. Alerta de proximidad Formato 29
        if (student.Formato29Deadline.HasValue)
        {
            var f29DaysLeft = (student.Formato29Deadline.Value - DateTime.UtcNow).TotalDays;
            if (f29DaysLeft <= 7)
            {
                bool hasF29 = await _context.Documents
                    .AnyAsync(d => d.ProjectId == project.Id && d.IsActive && d.DocumentType == DocumentType.Formato29);

                if (!hasF29)
                {
                    string statusText = f29DaysLeft < 0 ? "¡Fecha límite vencida!" : $"Vence en {Math.Ceiling(f29DaysLeft)} días.";
                    pending.Add(new PendingNotificationDto
                    {
                        Id = -102,
                        Title = "Entrega Formato 29 (Primer Reporte)",
                        Description = $"{statusText} Recuerda subir tu Formato 29 firmado antes de la fecha límite.",
                        ExpiresAt = student.Formato29Deadline.Value,
                        Type = "system",
                        Source = "Plazos de Evaluación",
                        ActionUrl = "/documents",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        // 3. Alerta de proximidad Formato 30
        if (student.Formato30Deadline.HasValue)
        {
            var f30DaysLeft = (student.Formato30Deadline.Value - DateTime.UtcNow).TotalDays;
            if (f30DaysLeft <= 7)
            {
                bool hasF30 = await _context.Documents
                    .AnyAsync(d => d.ProjectId == project.Id && d.IsActive && d.DocumentType == DocumentType.Formato30);

                if (!hasF30)
                {
                    string statusText = f30DaysLeft < 0 ? "¡Fecha límite vencida!" : $"Vence en {Math.Ceiling(f30DaysLeft)} días.";
                    pending.Add(new PendingNotificationDto
                    {
                        Id = -103,
                        Title = "Entrega Formato 30 (Segundo Reporte)",
                        Description = $"{statusText} Recuerda subir tu Formato 30 firmado antes de la fecha límite.",
                        ExpiresAt = student.Formato30Deadline.Value,
                        Type = "system",
                        Source = "Plazos de Evaluación",
                        ActionUrl = "/documents",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }
    }

    private static NotificationResponseDto MapToResponseDto(Notification n)
    {
        var roleList = (n.TargetRoles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var senderName = n.Sender != null
            ? $"{n.Sender.FirstName} {n.Sender.LastName} {n.Sender.LastName2}".Trim()
            : "Personal Académico";

        var targetUserName = n.TargetUser != null
            ? $"{n.TargetUser.FirstName} {n.TargetUser.LastName} {n.TargetUser.LastName2}".Trim()
            : null;

        return new NotificationResponseDto
        {
            Id = n.Id,
            Title = n.Title,
            Description = n.Description,
            ExpiresAt = n.ExpiresAt,
            TargetRoles = n.TargetRoles,
            TargetRolesList = roleList,
            CareerId = n.CareerId,
            CareerName = n.Career?.Name,
            ResidencyModality = n.ResidencyModality,
            TargetUserId = n.TargetUserId,
            TargetUserName = string.IsNullOrWhiteSpace(targetUserName) ? null : targetUserName,
            TargetUserEmail = n.TargetUser?.Email,
            SenderId = n.SenderId,
            SenderName = string.IsNullOrWhiteSpace(senderName) ? "Personal Académico" : senderName,
            Type = n.Type,
            CreatedAt = n.CreatedAt,
            ReadsCount = n.Reads?.Count ?? 0
        };
    }

    public async Task<Result<List<NotificationUserOptionDto>>> SearchUsersAsync(string? search, string? role)
    {
        var users = await _repository.SearchUsersAsync(search, role);
        return Result<List<NotificationUserOptionDto>>.Success(users);
    }

    private async Task EnqueueBroadcastEmailsAsync(CreateNotificationDto dto, Notification notification)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();
            var templateService = scope.ServiceProvider.GetRequiredService<IEmailTemplateService>();

            var portalUrl = "http://localhost:5085";

            // Si la notificación va dirigida a un usuario específico (pruebas o aviso unitario)
            if (dto.TargetUserId.HasValue && dto.TargetUserId.Value > 0)
            {
                var targetUser = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == dto.TargetUserId.Value && u.IsActive);

                if (targetUser != null && !string.IsNullOrWhiteSpace(targetUser.Email))
                {
                    var targetName = $"{targetUser.FirstName} {targetUser.LastName}".Trim();
                    var userLabel = string.IsNullOrWhiteSpace(targetName) ? targetUser.Email : $"{targetName} ({targetUser.Role})";

                    var emailMsg = templateService.BuildBroadcastNotificationEmail(
                        dto.Title.Trim(),
                        dto.Description.Trim(),
                        userLabel,
                        dto.ExpiresAt,
                        portalUrl,
                        new List<string> { targetUser.Email.Trim() }
                    );

                    emailQueue.Enqueue(emailMsg);
                    _logger.LogInformation("[CORREO INDIVIDUAL PRUEBAS] Notificación {Id} encolada para usuario {UserId} ({Email})",
                        notification.Id, targetUser.Id, targetUser.Email);
                }
                return;
            }

            var targetRoles = (dto.TargetRoles == null || dto.TargetRoles.Count == 0)
                ? new List<string> { "all" }
                : dto.TargetRoles.Select(r => r.Trim().ToLowerInvariant()).ToList();

            var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool includeAll = targetRoles.Contains("all");
            bool includeStudents = includeAll || targetRoles.Contains("student");
            bool includeAdvisors = includeAll || targetRoles.Contains("advisor");
            bool includeAcademic = includeAll || targetRoles.Contains("academic") || targetRoles.Contains("jefecarrera") || targetRoles.Contains("coordinadora") || targetRoles.Contains("vinculacion");
            bool includeDirector = includeAll || targetRoles.Contains("director") || targetRoles.Contains("admin");

            // 1. Estudiantes
            if (includeStudents)
            {
                var studentQuery = context.Students
                    .AsNoTracking()
                    .Include(s => s.User)
                    .Where(s => s.IsActive && s.User != null && s.User.IsActive && !string.IsNullOrWhiteSpace(s.User.Email));

                if (dto.CareerId.HasValue && dto.CareerId.Value > 0)
                {
                    studentQuery = studentQuery.Where(s => s.CareerId == dto.CareerId.Value);
                }

                if (!string.IsNullOrWhiteSpace(dto.ResidencyModality) && dto.ResidencyModality.Trim().ToLowerInvariant() != "all")
                {
                    var mod = dto.ResidencyModality.Trim().ToLowerInvariant();
                    if (mod is "acreditacion" or "innovatec" or "hackatec")
                    {
                        studentQuery = studentQuery.Where(s => context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.ProjectType != null &&
                            (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec") || p.ProjectType.ToLower().StartsWith("acreditacion"))));
                    }
                    else if (mod is "regular")
                    {
                        studentQuery = studentQuery.Where(s => !context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.ProjectType != null &&
                            (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec") || p.ProjectType.ToLower().StartsWith("acreditacion"))));
                    }
                }

                var studentEmails = await studentQuery.Select(s => s.User!.Email).ToListAsync();
                foreach (var em in studentEmails)
                {
                    if (!string.IsNullOrWhiteSpace(em)) emails.Add(em.Trim());
                }
            }

            // 2. Asesores
            if (includeAdvisors)
            {
                var advisorQuery = context.Advisors
                    .AsNoTracking()
                    .Include(a => a.User)
                    .Where(a => a.IsActive && a.User != null && a.User.IsActive && !string.IsNullOrWhiteSpace(a.User.Email));

                if (dto.CareerId.HasValue && dto.CareerId.Value > 0)
                {
                    advisorQuery = advisorQuery.Where(a => a.User!.CareerId == dto.CareerId.Value ||
                        context.UserCareers.Any(uc => uc.UserId == a.UserId && uc.CareerId == dto.CareerId.Value));
                }

                var advisorEmails = await advisorQuery.Select(a => a.User!.Email).ToListAsync();
                foreach (var em in advisorEmails)
                {
                    if (!string.IsNullOrWhiteSpace(em)) emails.Add(em.Trim());
                }
            }

            // 3. Personal Académico / Jefaturas / Coordinación
            if (includeAcademic)
            {
                var academicQuery = context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email) &&
                               (u.Role == UserRole.Academic || u.Role == UserRole.CareerHead || u.Role == UserRole.Coordinator ||
                                u.Role == UserRole.Vinculacion));

                if (dto.CareerId.HasValue && dto.CareerId.Value > 0)
                {
                    academicQuery = academicQuery.Where(u => u.CareerId == dto.CareerId.Value ||
                        context.UserCareers.Any(uc => uc.UserId == u.Id && uc.CareerId == dto.CareerId.Value));
                }

                var academicEmails = await academicQuery.Select(u => u.Email).ToListAsync();
                foreach (var em in academicEmails)
                {
                    if (!string.IsNullOrWhiteSpace(em)) emails.Add(em.Trim());
                }
            }

            // 4. Dirección / Admin (No tienen carrera asignada institucionalmente, no filtrar por CareerId)
            if (includeDirector)
            {
                var directorQuery = context.Users
                    .AsNoTracking()
                    .Where(u => u.IsActive && !string.IsNullOrWhiteSpace(u.Email) &&
                               (u.Role == UserRole.Director || u.Role == UserRole.Admin));

                var directorEmails = await directorQuery.Select(u => u.Email).ToListAsync();
                foreach (var em in directorEmails)
                {
                    if (!string.IsNullOrWhiteSpace(em)) emails.Add(em.Trim());
                }
            }

            if (emails.Count == 0)
            {
                _logger.LogInformation("[BROADCAST CORREO] Notificación {Id}: 0 destinatarios encontrados para los filtros seleccionados.", notification.Id);
                return;
            }

            var audienceLabel = FormatAudienceLabel(targetRoles);
            const int BatchSize = 300;
            var batches = emails.Chunk(BatchSize).ToList();

            _logger.LogInformation("[BROADCAST CORREO] Despachando notificación {Id} a {Total} destinatarios en {Batches} lote(s) de hasta {BatchSize} (BCC).",
                notification.Id, emails.Count, batches.Count, BatchSize);

            foreach (var batch in batches)
            {
                var emailMsg = templateService.BuildBroadcastNotificationEmail(
                    dto.Title.Trim(),
                    dto.Description.Trim(),
                    audienceLabel,
                    dto.ExpiresAt,
                    portalUrl,
                    batch.ToList()
                );

                emailQueue.Enqueue(emailMsg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERROR] Falla al encolar correos para notificación broadcast {Id}", notification.Id);
        }
    }

    private static string FormatAudienceLabel(List<string> roles)
    {
        if (roles.Contains("all")) return "Toda la Comunidad TecNM";
        var parts = new List<string>();
        if (roles.Contains("student")) parts.Add("Estudiantes");
        if (roles.Contains("advisor")) parts.Add("Asesores");
        if (roles.Contains("academic")) parts.Add("Personal Académico");
        if (roles.Contains("director")) parts.Add("Dirección");
        if (roles.Contains("company")) parts.Add("Empresas");
        return parts.Count > 0 ? string.Join(", ", parts) : "Comunidad TecNM";
    }
}
