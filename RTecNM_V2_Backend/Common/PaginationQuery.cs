namespace TecNM.Residency.Common;

public class PaginationQuery
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;
    private int _pageNumber = 1;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value < 1 ? 10 : Math.Min(value, MaxPageSize);
    }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }
}
