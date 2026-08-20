namespace TecNM.Residency.Searches.Dtos;

public class PagedSearchRequestDto
{
    public string SourceKey { get; set; } = string.Empty;
    public string SearchColumn { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public string MatchOption { get; set; } = "Contains"; // Contains, StartsWith, EndsWith, Exact
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortColumn { get; set; } = "id";
    public string SortDirection { get; set; } = "ASC"; // ASC, DESC
    public string StatusFilter { get; set; } = "active"; // active, inactive, all
}
