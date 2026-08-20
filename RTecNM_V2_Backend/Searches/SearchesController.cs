using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TecNM.Residency.Searches.Dtos;

namespace TecNM.Residency.Searches;

[ApiController]
[Route("api/v1/searches")]
[Authorize]
public class SearchesController : ControllerBase
{
    private readonly ISearchService _searchService;

    public SearchesController(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [HttpGet("sources")]
    public ActionResult<List<SearchSourceMetadataDto>> GetSources()
    {
        var sources = _searchService.GetAvailableSources();
        return Ok(sources);
    }

    [HttpPost("filter-paged")]
    public async Task<ActionResult<PagedSearchResponseDto>> FilterPaged([FromBody] PagedSearchRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceKey))
        {
            return BadRequest(new { message = "El parámetro 'sourceKey' es obligatorio." });
        }

        try
        {
            var response = await _searchService.SearchPagedAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
