using TecNM.Residency.Common;

namespace TecNM.Residency.Searches.Dtos;

public class PagedSearchResponseDto
{
    public SearchSourceMetadataDto Source { get; set; } = new();
    public PaginatedResult<Dictionary<string, object?>> Pagination { get; set; } = null!;
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public string? Warning { get; set; }
}
