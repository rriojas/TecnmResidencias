using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Students;

public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public StudentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Student>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false)
    {
        IQueryable<Student> q = _context.Students.Include(s => s.User).Include(s => s.Advisor);

        if (status == "active")
            q = q.Where(s => s.IsActive);
        else if (status == "inactive")
            q = q.Where(s => !s.IsActive);
        else if (!includeInactive)
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

    public async Task<List<Student>> GetAllForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false)
    {
        IQueryable<Student> q = _context.Students.Include(s => s.User).AsNoTracking();

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
        return await _context.Students
            .AsNoTracking()
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ToListAsync();
    }

    public async Task<Student?> GetByIdAsync(long id)
    {
        return await _context.Students
            .Include(s => s.User)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<Student?> GetByControlNumberAsync(string controlNumber)
    {
        var clean = (controlNumber ?? "").Trim().ToUpperInvariant();
        return await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ControlNumber.ToUpper() == clean);
    }

    public async Task<Student?> GetByUserIdAsync(long userId)
    {
        return await _context.Students
            .Include(s => s.User)
            .Include(s => s.Advisor)
            .FirstOrDefaultAsync(s => s.UserId == userId);
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
