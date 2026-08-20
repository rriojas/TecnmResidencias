using TecNM.Residency.Common;

namespace TecNM.Residency.Documents;

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(long id);
    Task<PaginatedResult<Document>> GetPagedByProjectIdAsync(long projectId, PaginationQuery query, bool includeInactive = false);
    Task AddAsync(Document document);
    Task UpdateAsync(Document document);
    Task SaveChangesAsync();
}
