using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Evaluations;

public class EvaluationRepository : IEvaluationRepository
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public EvaluationRepository(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Evaluation> SaveEvaluationAsync(Evaluation evaluation)
    {
        var existing = await _context.Evaluations
            .FirstOrDefaultAsync(e => e.ProjectId == evaluation.ProjectId 
                                   && e.EvaluationPeriod == evaluation.EvaluationPeriod 
                                   && e.IsActive);

        if (existing != null)
        {
            existing.Score = evaluation.Score;
            existing.Feedback = evaluation.Feedback;
            existing.EvaluatorId = evaluation.EvaluatorId;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = evaluation.UpdatedBy;
            _context.Evaluations.Update(existing);
            await _context.SaveChangesAsync();
            return existing;
        }

        _context.Evaluations.Add(evaluation);
        await _context.SaveChangesAsync();
        return evaluation;
    }

    public async Task<Evaluation?> GetEvaluationByIdAsync(long id)
    {
        return await _context.Evaluations
            .Include(e => e.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<bool> SoftDeleteEvaluationAsync(long id, long? deletedBy)
    {
        var eval = await _context.Evaluations.FirstOrDefaultAsync(e => e.Id == id);
        if (eval == null || !eval.IsActive) return false;

        eval.IsActive = false;
        eval.DeletedAt = DateTime.UtcNow;
        eval.DeletedBy = deletedBy;
        eval.UpdatedAt = DateTime.UtcNow;
        eval.UpdatedBy = deletedBy;
        _context.Evaluations.Update(eval);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaginatedResult<Evaluation>> GetEvaluationsByProjectIdPagedAsync(long projectId, PaginationQuery query)
    {
        var q = _context.Evaluations
            .Include(e => e.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Where(e => e.ProjectId == projectId && e.IsActive)
            .OrderBy(e => e.CreatedAt);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<AdvisorySession> CreateAdvisorySessionAsync(AdvisorySession session)
    {
        _context.AdvisorySessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<AdvisorySession?> GetSessionByIdAsync(long id)
    {
        var session = await _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue
            && session.Project?.Student != null && session.Project.Student.CareerId != _currentUser.CareerId.Value)
            return null;

        return session;
    }

    public async Task UpdateSessionAsync(AdvisorySession session)
    {
        _context.AdvisorySessions.Update(session);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> SoftDeleteSessionAsync(long id, long? deletedBy)
    {
        var session = await _context.AdvisorySessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session == null || !session.IsActive) return false;

        session.IsActive = false;
        session.DeletedAt = DateTime.UtcNow;
        session.DeletedBy = deletedBy;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = deletedBy;
        _context.AdvisorySessions.Update(session);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaginatedResult<AdvisorySession>> GetAdvisorySessionsByProjectIdPagedAsync(long projectId, PaginationQuery query, bool includeInactive = false)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor)
            .Where(s => s.ProjectId == projectId);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "SessionDate", "CreatedAt" },
            "SessionDate", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<PaginatedResult<AdvisorySession>> GetAdvisorySessionsPagedAsync(PaginationQuery query, long? projectId, bool includeInactive = false)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        if (projectId.HasValue)
            q = q.Where(s => s.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "SessionDate", "CreatedAt" },
            "SessionDate", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<AdvisorySession>> GetAllSessionsForExportAsync(long? projectId, string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                .ThenInclude(s => s!.User)
            .Include(s => s.Advisor)
            .AsNoTracking();

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        if (projectId.HasValue)
            q = q.Where(s => s.ProjectId == projectId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term)));
        }

        q = q.ApplySort(sortBy, sortDir,
            new[] { "SessionDate", "CreatedAt" },
            "SessionDate", defaultDescending: true);

        return await q.Take(1000).ToListAsync();
    }

    public async Task<PaginatedResult<AdvisorySession>> GetAdvisoryTimelinePagedAsync(AdvisoryTimelineQuery query)
    {
        IQueryable<AdvisorySession> q = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
                    .ThenInclude(st => st!.User)
            .Include(s => s.Project)
                .ThenInclude(p => p!.Company)
            .Include(s => s.Advisor)
                .ThenInclude(a => a!.User);

        // RBAC: Jefe de Carrera sólo puede consultar de su carrera
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == _currentUser.CareerId.Value);
        }
        else if (query.CareerId.HasValue && query.CareerId.Value > 0)
        {
            q = q.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == query.CareerId.Value);
        }

        if (query.AdvisorId.HasValue && query.AdvisorId.Value > 0)
        {
            q = q.Where(s => s.AdvisorId == query.AdvisorId.Value);
        }

        if (query.StartDate.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(query.StartDate.Value.Date, DateTimeKind.Utc);
            q = q.Where(s => s.SessionDate >= startUtc);
        }

        if (query.EndDate.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(query.EndDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            q = q.Where(s => s.SessionDate <= endUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.ObservationFilter) && query.ObservationFilter.ToLowerInvariant() != "all")
        {
            var obsFilter = query.ObservationFilter.Trim().ToLowerInvariant();
            if (obsFilter == "with_notes")
            {
                q = q.Where(s => s.ReviewNotes != null && s.ReviewNotes != "");
            }
            else if (obsFilter == "without_notes")
            {
                q = q.Where(s => s.ReviewNotes == null || s.ReviewNotes == "");
            }
        }

        if (!string.IsNullOrWhiteSpace(query.ProjectStatus) && query.ProjectStatus.ToLowerInvariant() != "all")
        {
            var pStatus = query.ProjectStatus.Trim().ToLowerInvariant();
            if (Enum.TryParse<ProjectStatus>(pStatus, true, out var parsedStatus))
            {
                q = q.Where(s => s.Project != null && s.Project.Status == parsedStatus);
            }
        }

        if (!query.IncludeInactive)
        {
            q = q.Where(s => s.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.TopicsCovered.ToLower().Contains(term)
                             || (s.StudentAgreements != null && s.StudentAgreements.ToLower().Contains(term))
                             || (s.ReviewNotes != null && s.ReviewNotes.ToLower().Contains(term))
                             || (s.Project != null && s.Project.Title.ToLower().Contains(term))
                             || (s.Advisor != null && s.Advisor.FullName.ToLower().Contains(term))
                             || (s.Project != null && s.Project.Student != null &&
                                 (s.Project.Student.FirstName.ToLower().Contains(term)
                                  || s.Project.Student.LastName.ToLower().Contains(term)
                                  || s.Project.Student.ControlNumber.ToLower().Contains(term))));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "SessionDate", "CreatedAt", "AdvisorId" },
            "SessionDate", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<AdvisoryTimelineSummaryDto> GetAdvisorsHealthStatusAsync(long? careerId)
    {
        long? effectiveCareerId = careerId;
        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            effectiveCareerId = _currentUser.CareerId.Value;
        }

        // Proyectos operativos activos (con dictamen aprobado, en progreso o completado)
        var operationalProjects = await _context.Projects
            .Where(p => p.IsActive && (p.Status == ProjectStatus.Approved || p.Status == ProjectStatus.InProgress || p.Status == ProjectStatus.Completed))
            .Select(p => new { p.Id, p.StudentId, p.Title, p.Status })
            .ToListAsync();

        var studentIdsWithApprovedProjects = operationalProjects.Select(p => p.StudentId).ToHashSet();
        var projectsByStudent = operationalProjects
            .GroupBy(p => p.StudentId)
            .ToDictionary(g => g.Key, g => g.FirstOrDefault());

        // Estudiantes activos con asesor asignado y con anteproyecto formalmente aprobado o en curso
        var studentsQuery = _context.Students
            .Include(s => s.Advisor)
            .Where(s => s.IsActive && s.AdvisorId.HasValue && studentIdsWithApprovedProjects.Contains(s.Id));

        if (effectiveCareerId.HasValue && effectiveCareerId.Value > 0)
        {
            studentsQuery = studentsQuery.Where(s => s.CareerId == effectiveCareerId.Value);
        }

        var activeStudents = await studentsQuery.ToListAsync();
        var advisorIdsWithStudents = activeStudents.Select(s => s.AdvisorId!.Value).Distinct().ToList();

        // Asesores relevantes
        var advisorsQuery = _context.Advisors
            .Include(a => a.User)
            .Where(a => a.IsActive && (advisorIdsWithStudents.Contains(a.Id) || (!effectiveCareerId.HasValue && a.DepartmentId > 0)));

        var advisors = await advisorsQuery.ToListAsync();
        if (advisors.Count == 0 && advisorIdsWithStudents.Count > 0)
        {
            advisors = await _context.Advisors.Where(a => advisorIdsWithStudents.Contains(a.Id)).ToListAsync();
        }

        var careers = await _context.Careers.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);

        var sessionQuery = _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
            .Where(s => s.IsActive && s.Project != null && (s.Project.Status == ProjectStatus.Approved || s.Project.Status == ProjectStatus.InProgress || s.Project.Status == ProjectStatus.Completed));

        if (effectiveCareerId.HasValue && effectiveCareerId.Value > 0)
        {
            sessionQuery = sessionQuery.Where(s => s.Project != null && s.Project.Student != null && s.Project.Student.CareerId == effectiveCareerId.Value);
        }

        var allSessions = await sessionQuery.ToListAsync();

        var metrics = new List<AdvisorHealthMetricDto>();
        var now = DateTime.UtcNow;

        foreach (var adv in advisors)
        {
            var myStudents = activeStudents.Where(s => s.AdvisorId == adv.Id).ToList();
            var mySessions = allSessions.Where(s => s.AdvisorId == adv.Id).ToList();

            var totalResidents = myStudents.Count;
            var totalSessions = mySessions.Count;
            var lastSession = mySessions.OrderByDescending(s => s.SessionDate).FirstOrDefault();
            var lastSessionDate = lastSession?.SessionDate;

            int daysWithoutActivity = 0;
            if (lastSessionDate.HasValue)
            {
                daysWithoutActivity = (int)(now.Date - lastSessionDate.Value.Date).TotalDays;
            }
            else if (totalResidents > 0)
            {
                var oldestAssignment = myStudents.Where(s => s.AdvisorAssignedAt.HasValue)
                    .Select(s => s.AdvisorAssignedAt!.Value)
                    .DefaultIfEmpty(adv.CreatedAt)
                    .Min();
                daysWithoutActivity = Math.Max(15, (int)(now.Date - oldestAssignment.Date).TotalDays);
            }

            var groupedByCreatedDay = mySessions
                .GroupBy(s => s.CreatedAt.Date)
                .Any(g => g.Count() >= 3 && g.Select(s => s.SessionDate.Date).Distinct().Count() >= 3);

            string healthStatus;
            string healthLabel;
            string alertMessage;

            if (totalResidents == 0)
            {
                healthStatus = "healthy";
                healthLabel = "Sin Residentes en Curso";
                alertMessage = "El asesor no tiene alumnos con anteproyecto aprobado en curso actualmente.";
            }
            else if (totalSessions == 0)
            {
                healthStatus = "critical";
                healthLabel = "Sin Asesorías Registradas";
                alertMessage = $"Tiene {totalResidents} alumno(s) asignado(s) pero no ha registrado ninguna asesoría ({daysWithoutActivity} días).";
            }
            else if (daysWithoutActivity > 21)
            {
                healthStatus = "critical";
                healthLabel = "Inactividad Crítica (> 21 días)";
                alertMessage = $"Han transcurrido {daysWithoutActivity} días sin registrar asesorías con sus {totalResidents} residente(s).";
            }
            else if (daysWithoutActivity >= 15)
            {
                healthStatus = "warning";
                healthLabel = "Alerta Preventiva (15-21 días)";
                alertMessage = $"Lleva {daysWithoutActivity} días sin registrar sesión. Conviene dar seguimiento preventivo.";
            }
            else if (groupedByCreatedDay)
            {
                healthStatus = "irregular";
                healthLabel = "Seguimiento Irregular";
                alertMessage = "Se detectó captura atípica de múltiples sesiones en una misma fecha.";
            }
            else if (daysWithoutActivity < 0)
            {
                healthStatus = "healthy";
                healthLabel = "Al Día (Programada)";
                alertMessage = $"Tiene asesoría programada para el {lastSessionDate!.Value:dd/MM/yyyy}.";
            }
            else
            {
                healthStatus = "healthy";
                healthLabel = "Al Día (< 15 días)";
                alertMessage = daysWithoutActivity switch
                {
                    0 => "Sesión registrada el día de hoy.",
                    1 => "Última sesión registrada el día de ayer.",
                    _ => $"Seguimiento constante (hace {daysWithoutActivity} días)."
                };
            }

            var deptName = adv.DepartmentId > 0 && careers.TryGetValue(adv.DepartmentId, out var dn) ? dn : "División Académica";

            // Detalle por alumno asignado
            var studentTimelines = new List<AdvisorStudentTimelineDto>();
            foreach (var st in myStudents)
            {
                projectsByStudent.TryGetValue(st.Id, out var proj);
                var studentSessions = mySessions
                    .Where(s => s.Project != null && s.Project.StudentId == st.Id)
                    .OrderBy(s => s.SessionDate)
                    .ToList();

                var stLastSession = studentSessions.LastOrDefault();
                int stDays = 0;
                string stHealth;
                string stAlert;

                if (stLastSession != null)
                {
                    stDays = (int)(now.Date - stLastSession.SessionDate.Date).TotalDays;
                    if (stDays > 21)
                    {
                        stHealth = "critical";
                        stAlert = $"{stDays} días sin asesoría (atención urgente)";
                    }
                    else if (stDays >= 15)
                    {
                        stHealth = "warning";
                        stAlert = $"{stDays} días sin asesoría (alerta preventiva)";
                    }
                    else if (stDays < 0)
                    {
                        stHealth = "healthy";
                        stAlert = $"Sesión programada para dentro de {Math.Abs(stDays)} día(s) ({stLastSession.SessionDate:dd/MM/yyyy})";
                    }
                    else if (stDays == 0)
                    {
                        stHealth = "healthy";
                        stAlert = "Sesión realizada el día de hoy";
                    }
                    else if (stDays == 1)
                    {
                        stHealth = "healthy";
                        stAlert = "Última sesión realizada el día de ayer";
                    }
                    else
                    {
                        stHealth = "healthy";
                        stAlert = $"Al corriente (última sesión hace {stDays} días)";
                    }
                }
                else
                {
                    var assignDate = st.AdvisorAssignedAt ?? adv.CreatedAt;
                    stDays = Math.Max(15, (int)(now.Date - assignDate.Date).TotalDays);
                    stHealth = "critical";
                    stAlert = $"Sin asesorías registradas ({stDays} días desde asignación)";
                }

                var sessionDots = studentSessions.Select((s, index) => new StudentAdvisoryDotDto
                {
                    Id = s.Id,
                    SessionNumber = index + 1,
                    SessionDate = s.SessionDate,
                    TopicsCovered = s.TopicsCovered,
                    StudentAgreements = s.StudentAgreements,
                    CreatedAt = s.CreatedAt,
                    SupervisionNotes = s.ReviewNotes,
                    SupervisedAt = s.ReviewedAt
                }).ToList();

                studentTimelines.Add(new AdvisorStudentTimelineDto
                {
                    StudentId = st.Id,
                    StudentName = $"{st.FirstName} {st.LastName}".Trim(),
                    StudentControlNumber = st.ControlNumber,
                    ProjectId = proj?.Id,
                    ProjectTitle = proj?.Title ?? "Sin anteproyecto registrado",
                    ProjectStatus = proj?.Status.ToString() ?? "none",
                    TotalSessions = studentSessions.Count,
                    LastSessionDate = stLastSession?.SessionDate,
                    DaysWithoutActivity = stDays,
                    HealthStatus = stHealth,
                    AlertMessage = stAlert,
                    Sessions = sessionDots
                });
            }

            metrics.Add(new AdvisorHealthMetricDto
            {
                AdvisorId = adv.Id,
                AdvisorName = adv.FullName,
                AdvisorTitle = adv.Title,
                AdvisorEmail = adv.User?.Email,
                DepartmentId = adv.DepartmentId,
                DepartmentName = deptName,
                TotalAssignedResidents = totalResidents,
                TotalSessions = totalSessions,
                LastSessionDate = lastSessionDate,
                DaysWithoutActivity = daysWithoutActivity,
                HealthStatus = healthStatus,
                HealthLabel = healthLabel,
                AlertMessage = alertMessage,
                Students = studentTimelines
            });
        }

        metrics = metrics
            .OrderBy(m => m.HealthStatus switch { "critical" => 1, "warning" => 2, "irregular" => 3, _ => 4 })
            .ThenByDescending(m => m.DaysWithoutActivity)
            .ToList();

        var observedSessionsCount = allSessions.Count(s => !string.IsNullOrWhiteSpace(s.ReviewNotes));

        return new AdvisoryTimelineSummaryDto
        {
            TotalAdvisors = metrics.Count,
            HealthyCount = metrics.Count(m => m.HealthStatus == "healthy"),
            WarningCount = metrics.Count(m => m.HealthStatus == "warning"),
            CriticalCount = metrics.Count(m => m.HealthStatus == "critical"),
            IrregularCount = metrics.Count(m => m.HealthStatus == "irregular"),
            TotalSessions = allSessions.Count,
            ObservedSessionsCount = observedSessionsCount,
            AdvisorHealthMetrics = metrics
        };
    }

    public async Task<bool> SaveSupervisionNoteAsync(long id, string? notes, long supervisorId)
    {
        var session = await _context.AdvisorySessions
            .Include(s => s.Project)
                .ThenInclude(p => p!.Student)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);

        if (session == null) return false;

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            if (session.Project?.Student == null || session.Project.Student.CareerId != _currentUser.CareerId.Value)
            {
                return false;
            }
        }

        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        session.ReviewNotes = trimmedNotes;
        session.ReviewStatus = trimmedNotes != null ? "observed" : "pending";
        session.ReviewedAt = trimmedNotes != null ? DateTime.UtcNow : null;
        session.ReviewedBy = trimmedNotes != null ? supervisorId : null;
        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = supervisorId;

        _context.AdvisorySessions.Update(session);
        await _context.SaveChangesAsync();
        return true;
    }
}
