using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Auth;
using TecNM.Residency.Common;
using TecNM.Residency.Projects;

namespace TecNM.Residency.Students;

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public StudentRepository(AppDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<PaginatedResult<Student>> GetPagedAsync(
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
        IQueryable<Student> q = _context.Students.Include(s => s.User).Include(s => s.Advisor)
            .Where(s => s.User == null || s.User.Role == UserRole.Student);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.CareerId == _currentUser.CareerId.Value);
        }
        else if (_currentUser.Role == UserRole.Coordinator)
        {
            var allowedCareerIds = _currentUser.CareerIds;
            if (careerId.HasValue && careerId.Value > 0)
            {
                q = allowedCareerIds.Contains(careerId.Value)
                    ? q.Where(s => s.CareerId == careerId.Value)
                    : q.Where(s => false);
            }
            else
            {
                q = q.Where(s => allowedCareerIds.Contains(s.CareerId));
            }
        }
        else if (careerId.HasValue && careerId.Value > 0)
        {
            q = q.Where(s => s.CareerId == careerId.Value);
        }

        if (onlyApprovedProject)
        {
            var approvedStatuses = new[] { ProjectStatus.Approved, ProjectStatus.InProgress, ProjectStatus.Completed };
            var approvedStudentIds = _context.Projects
                .Where(p => p.IsActive && approvedStatuses.Contains(p.Status))
                .Select(p => p.StudentId);
            q = q.Where(s => approvedStudentIds.Contains(s.Id));
        }

        if (excludeEvaluated)
        {
            var completedStudentIds = _context.Projects
                .Where(p => p.IsActive && p.Status == ProjectStatus.Completed)
                .Select(p => p.StudentId);
            q = q.Where(s => !completedStudentIds.Contains(s.Id));
        }

        if (status == "active")
            q = q.Where(s => s.IsActive);
        else if (status == "inactive")
            q = q.Where(s => !s.IsActive);
        else if (!includeInactive && status != "all")
            q = q.Where(s => s.IsActive);

        q = ApplyCustomFilters(q, assignmentStatus, acceptanceLetterStatus, residencyStage);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.ControlNumber.ToLower().Contains(term)
                             || s.FirstName.ToLower().Contains(term)
                             || s.LastName.ToLower().Contains(term)
                             || (s.User != null && s.User.Email.ToLower().Contains(term)));
        }

        var sortBy = query.SortBy;
        var isDesc = (query.SortDir ?? "").Equals("desc", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(sortBy, "FullName", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => s.FirstName).ThenByDescending(s => s.LastName)
                : q.OrderBy(s => s.FirstName).ThenBy(s => s.LastName);
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }
        if (string.Equals(sortBy, "Email", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => s.User != null ? s.User.Email : "")
                : q.OrderBy(s => s.User != null ? s.User.Email : "");
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }
        if (string.Equals(sortBy, "AdvisorName", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => s.Advisor != null ? s.Advisor.FullName : "")
                : q.OrderBy(s => s.Advisor != null ? s.Advisor.FullName : "");
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }
        if (string.Equals(sortBy, "AssignmentStatus", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sortBy, "AdvisorStatus", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sortBy, "AdvisorId", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => s.AdvisorId.HasValue).ThenByDescending(s => s.Advisor != null ? s.Advisor.FullName : "")
                : q.OrderBy(s => s.AdvisorId.HasValue).ThenBy(s => s.Advisor != null ? s.Advisor.FullName : "");
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }
        if (string.Equals(sortBy, "ProjectTitle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sortBy, "Project", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive).Select(p => p.Title).FirstOrDefault() ?? "").ThenBy(s => s.FirstName)
                : q.OrderBy(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive).Select(p => p.Title).FirstOrDefault() ?? "").ThenBy(s => s.FirstName);
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }
        if (string.Equals(sortBy, "HasAcceptanceLetter", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sortBy, "AcceptanceLetter", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive)
                    .Any(p => _context.Documents.Any(d => d.ProjectId == p.Id && d.IsActive && 
                        (d.DocumentType == "CartaAceptacion" || d.DocumentType == "carta_aceptacion" || 
                         d.DocumentType == "CartaAprobacion" || d.DocumentType == "carta_aprobacion" ||
                         d.DocumentType == "ConstanciaAcreditacion" || d.DocumentType == "constancia_acreditacion")))).ThenBy(s => s.FirstName)
                : q.OrderBy(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive)
                    .Any(p => _context.Documents.Any(d => d.ProjectId == p.Id && d.IsActive && 
                        (d.DocumentType == "CartaAceptacion" || d.DocumentType == "carta_aceptacion" || 
                         d.DocumentType == "CartaAprobacion" || d.DocumentType == "carta_aprobacion" ||
                         d.DocumentType == "ConstanciaAcreditacion" || d.DocumentType == "constancia_acreditacion")))).ThenBy(s => s.FirstName);
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }
        if (string.Equals(sortBy, "ResidencyStage", StringComparison.OrdinalIgnoreCase))
        {
            q = isDesc
                ? q.OrderByDescending(s => s.AdvisorId.HasValue)
                   .ThenByDescending(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive).Select(p => (int)p.Status).FirstOrDefault())
                : q.OrderBy(s => s.AdvisorId.HasValue)
                   .ThenBy(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive).Select(p => (int)p.Status).FirstOrDefault());
            return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
        }

        q = q.ApplySort(sortBy, query.SortDir,
            new[] { "ControlNumber", "FirstName", "LastName", "Gpa", "CreatedAt", "CareerId", "IsActive", "IsPresentationLetterSent", "AdvisorId" },
            "CreatedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Student>> GetAllForExportAsync(
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
        IQueryable<Student> q = _context.Students.Include(s => s.User).AsNoTracking()
            .Where(s => s.User == null || s.User.Role == UserRole.Student);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.CareerId == _currentUser.CareerId.Value);
        }
        else if (_currentUser.Role == UserRole.Coordinator)
        {
            var allowedCareerIds = _currentUser.CareerIds;
            if (careerId.HasValue && careerId.Value > 0)
            {
                q = allowedCareerIds.Contains(careerId.Value)
                    ? q.Where(s => s.CareerId == careerId.Value)
                    : q.Where(s => false);
            }
            else
            {
                q = q.Where(s => allowedCareerIds.Contains(s.CareerId));
            }
        }
        else if (careerId.HasValue && careerId.Value > 0)
        {
            q = q.Where(s => s.CareerId == careerId.Value);
        }

        if (onlyApprovedProject)
        {
            var approvedStatuses = new[] { ProjectStatus.Approved, ProjectStatus.InProgress, ProjectStatus.Completed };
            var approvedStudentIds = _context.Projects
                .Where(p => p.IsActive && approvedStatuses.Contains(p.Status))
                .Select(p => p.StudentId);
            q = q.Where(s => approvedStudentIds.Contains(s.Id));
        }

        if (excludeEvaluated)
        {
            var completedStudentIds = _context.Projects
                .Where(p => p.IsActive && p.Status == ProjectStatus.Completed)
                .Select(p => p.StudentId);
            q = q.Where(s => !completedStudentIds.Contains(s.Id));
        }

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

        q = ApplyCustomFilters(q, assignmentStatus, acceptanceLetterStatus, residencyStage);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(s => s.ControlNumber.ToLower().Contains(term)
                             || s.FirstName.ToLower().Contains(term)
                             || s.LastName.ToLower().Contains(term)
                             || (s.User != null && s.User.Email.ToLower().Contains(term)));
        }

        q = q.ApplySort(sortBy, sortDir,
            new[] { "ControlNumber", "FirstName", "LastName", "Gpa", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.Take(1000).ToListAsync();
    }

    private IQueryable<Student> ApplyCustomFilters(
        IQueryable<Student> q,
        string? assignmentStatus,
        string? acceptanceLetterStatus,
        string? residencyStage)
    {
        if (!string.IsNullOrWhiteSpace(assignmentStatus) && !assignmentStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (assignmentStatus.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => !s.AdvisorId.HasValue);
            }
            else if (assignmentStatus.Equals("assigned", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => s.AdvisorId.HasValue);
            }
        }

        if (!string.IsNullOrWhiteSpace(acceptanceLetterStatus) && !acceptanceLetterStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (acceptanceLetterStatus.Equals("uploaded", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive)
                    .Any(p => _context.Documents.Any(d => d.ProjectId == p.Id && d.IsActive &&
                        (d.DocumentType == "CartaAceptacion" || d.DocumentType == "carta_aceptacion" ||
                         d.DocumentType == "CartaAprobacion" || d.DocumentType == "carta_aprobacion" ||
                         d.DocumentType == "ConstanciaAcreditacion" || d.DocumentType == "constancia_acreditacion"))));
            }
            else if (acceptanceLetterStatus.Equals("exempt", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => _context.Projects.Where(p => p.StudentId == s.Id && p.IsActive)
                    .Any(p => (p.ProjectType != null && (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec") || p.ProjectType.ToLower().StartsWith("acreditacion"))) ||
                              (p.Title != null && (p.Title.ToLower().Contains("innovatec") || p.Title.ToLower().Contains("hackatec")))));
            }
            else if (acceptanceLetterStatus.Equals("pending", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => !_context.Projects.Where(p => p.StudentId == s.Id && p.IsActive)
                    .Any(p => _context.Documents.Any(d => d.ProjectId == p.Id && d.IsActive &&
                        (d.DocumentType == "CartaAceptacion" || d.DocumentType == "carta_aceptacion" ||
                         d.DocumentType == "CartaAprobacion" || d.DocumentType == "carta_aprobacion" ||
                         d.DocumentType == "ConstanciaAcreditacion" || d.DocumentType == "constancia_acreditacion"))) &&
                    !_context.Projects.Where(p => p.StudentId == s.Id && p.IsActive)
                    .Any(p => (p.ProjectType != null && (p.ProjectType.ToLower().Contains("innovatec") || p.ProjectType.ToLower().Contains("hackatec") || p.ProjectType.ToLower().StartsWith("acreditacion"))) ||
                              (p.Title != null && (p.Title.ToLower().Contains("innovatec") || p.Title.ToLower().Contains("hackatec")))));
            }
        }

        if (!string.IsNullOrWhiteSpace(residencyStage) && !residencyStage.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var stage = residencyStage.Trim();
            if (stage.Equals("Sin Anteproyecto", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => !_context.Projects.Any(p => p.StudentId == s.Id && p.IsActive));
            }
            else if (stage.Equals("Borrador", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.Status == ProjectStatus.Draft));
            }
            else if (stage.Equals("Anteproyecto Registrado", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive &&
                    (p.Status == ProjectStatus.Pending || p.Status == ProjectStatus.Proposed || p.Status == ProjectStatus.UnderReview)));
            }
            else if (stage.Equals("Dictamen Aprobado", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => !s.AdvisorId.HasValue && _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.Status == ProjectStatus.Approved));
            }
            else if (stage.Equals("Asesor Asignado", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => s.AdvisorId.HasValue);
            }
            else if (stage.Equals("En Residencia", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => s.AdvisorId.HasValue && _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive &&
                    (p.Status == ProjectStatus.Approved || p.Status == ProjectStatus.InProgress)));
            }
            else if (stage.Equals("Con Observaciones", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.Status == ProjectStatus.Rejected));
            }
            else if (stage.Equals("Concluido / Evaluado", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(s => _context.Projects.Any(p => p.StudentId == s.Id && p.IsActive && p.Status == ProjectStatus.Completed));
            }
        }

        return q;
    }

    public async Task<List<Student>> GetOptionsAsync()
    {
        IQueryable<Student> q = _context.Students
            .Include(s => s.User)
            .AsNoTracking()
            .Where(s => s.User == null || s.User.Role == UserRole.Student);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.CareerId == _currentUser.CareerId.Value);
        }
        else if (_currentUser.Role == UserRole.Coordinator)
        {
            var allowedCareerIds = _currentUser.CareerIds;
            q = q.Where(s => allowedCareerIds.Contains(s.CareerId));
        }

        return await q.OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public async Task<Student?> GetByIdAsync(long id)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.Id == id && (s.User == null || s.User.Role == UserRole.Student));

        if (student != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && student.CareerId != _currentUser.CareerId.Value)
            return null;

        if (student != null && _currentUser.Role == UserRole.Coordinator && !_currentUser.CareerIds.Contains(student.CareerId))
            return null;

        return student;
    }

    public async Task<Student?> GetByControlNumberAsync(string controlNumber)
    {
        var clean = (controlNumber ?? "").Trim().ToUpperInvariant();
        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ControlNumber.ToUpper() == clean && (s.User == null || s.User.Role == UserRole.Student));

        if (student != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && student.CareerId != _currentUser.CareerId.Value)
            return null;

        if (student != null && _currentUser.Role == UserRole.Coordinator && !_currentUser.CareerIds.Contains(student.CareerId))
            return null;

        return student;
    }

    public async Task<Student?> GetByUserIdAsync(long userId)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.UserId == userId && (s.User == null || s.User.Role == UserRole.Student));

        if (student != null && _currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue && student.CareerId != _currentUser.CareerId.Value)
            return null;

        if (student != null && _currentUser.Role == UserRole.Coordinator && !_currentUser.CareerIds.Contains(student.CareerId))
            return null;

        return student;
    }

    public async Task<Student> AddAsync(Student student)
    {
        await _context.Students.AddAsync(student);
        await _context.SaveChangesAsync();
        return student;
    }

    public async Task UpdateAsync(Student student)
    {
        _context.Students.Update(student);
        await _context.SaveChangesAsync();
    }

    public async Task<StudentBlock?> GetActiveBlockAsync(long studentId)
    {
        return await _context.StudentBlocks
            .Where(b => b.StudentId == studentId && b.IsActive)
            .OrderByDescending(b => b.BlockedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<StudentBlock> AddBlockAsync(StudentBlock block)
    {
        await _context.StudentBlocks.AddAsync(block);
        await _context.SaveChangesAsync();
        return block;
    }

    public async Task UpdateBlockAsync(StudentBlock block)
    {
        _context.StudentBlocks.Update(block);
        await _context.SaveChangesAsync();
    }

    public async Task<List<StudentBlock>> GetBlockHistoryAsync(long studentId)
    {
        return await _context.StudentBlocks
            .Where(b => b.StudentId == studentId)
            .OrderByDescending(b => b.BlockedAt)
            .ToListAsync();
    }
}
