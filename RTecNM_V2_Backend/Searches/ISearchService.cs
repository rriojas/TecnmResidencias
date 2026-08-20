using TecNM.Residency.Searches.Dtos;

namespace TecNM.Residency.Searches;

public interface ISearchService
{
    List<SearchSourceMetadataDto> GetAvailableSources();
    Task<PagedSearchResponseDto> SearchPagedAsync(PagedSearchRequestDto request, CancellationToken cancellationToken = default);
}
