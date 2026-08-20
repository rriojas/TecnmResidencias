using TecNM.Residency.Common;

namespace TecNM.Residency.Students;

public interface IStudentRepository
{
    Task<PaginatedResult<Student>> GetPagedAsync(PaginationQuery query, string? status, bool includeInactive = false);
    Task<List<Student>> GetAllForExportAsync(string? search, string? sortBy, string? sortDir, bool includeInactive = false);
    Task<List<Student>> GetOptionsAsync();
    Task<Student?> GetByIdAsync(long id);
    Task<Student?> GetByControlNumberAsync(string controlNumber);
    Task<Student?> GetByUserIdAsync(long userId);
    Task<Student> AddAsync(Student student);
    Task UpdateAsync(Student student);
}
