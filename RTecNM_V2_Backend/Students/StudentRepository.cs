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

    public async Task<PaginatedResult<Student>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false, bool onlyApprovedProject = false, long? careerId = null)
    {
        IQueryable<Student> q = _context.Students.Include(s => s.User).Include(s => s.Advisor)
            .Where(s => s.User == null || s.User.Role == UserRole.Student);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.CareerId == _currentUser.CareerId.Value);
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

        if (status == "active")
            q = q.Where(s => s.IsActive);
        else if (status == "inactive")
            q = q.Where(s => !s.IsActive);
        else if (!includeInactive && status != "all")
            q = q.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(s => s.ControlNumber.ToLower().Contains(term)
                             || s.FirstName.ToLower().Contains(term)
                             || s.LastName.ToLower().Contains(term)
                             || (s.User != null && s.User.Email.ToLower().Contains(term)));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "ControlNumber", "FirstName", "LastName", "Gpa", "CreatedAt" },
            "CreatedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task<List<Student>> GetAllForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false, bool onlyApprovedProject = false, long? careerId = null)
    {
        IQueryable<Student> q = _context.Students.Include(s => s.User).AsNoTracking()
            .Where(s => s.User == null || s.User.Role == UserRole.Student);

        if (_currentUser.Role == UserRole.CareerHead && _currentUser.CareerId.HasValue)
        {
            q = q.Where(s => s.CareerId == _currentUser.CareerId.Value);
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

        if (!includeInactive)
            q = q.Where(s => s.IsActive);

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
}
