using Microsoft.EntityFrameworkCore;
using TecNM.Residency.Common;

namespace TecNM.Residency.Documents;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _context;

    public DocumentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Document?> GetByIdAsync(long id)
    {
        return await _context.Set<Document>()
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<PaginatedResult<Document>> GetPagedByProjectIdAsync(long projectId, PaginationQuery query, bool includeInactive = false)
    {
        IQueryable<Document> q = _context.Set<Document>()
            .Where(d => d.ProjectId == projectId);

        if (!includeInactive)
            q = q.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            q = q.Where(d => d.FileName.ToLower().Contains(term) || d.DocumentType.ToLower().Contains(term));
        }

        q = q.ApplySort(query.SortBy, query.SortDir,
            new[] { "FileName", "DocumentType", "UploadedAt" },
            "UploadedAt", defaultDescending: true);

        return await q.ToPaginatedAsync(query.PageNumber, query.PageSize);
    }

    public async Task AddAsync(Document document)
    {
        await _context.Set<Document>().AddAsync(document);
    }

    public async Task UpdateAsync(Document document)
    {
        _context.Set<Document>().Update(document);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
