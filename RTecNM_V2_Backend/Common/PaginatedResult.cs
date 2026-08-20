namespace TecNM.Residency.Common;

public class PaginatedResult<T>
{
    public IEnumerable<T> Items { get; private set; } = Enumerable.Empty<T>();
    public int TotalCount { get; private set; }
    public int PageNumber { get; private set; }
    public int PageSize { get; private set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    private PaginatedResult() { }

    public static PaginatedResult<T> Create(
        IEnumerable<T> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        return new PaginatedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
