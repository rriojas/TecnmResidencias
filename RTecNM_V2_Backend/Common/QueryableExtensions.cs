using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace TecNM.Residency.Common;

public static class QueryableExtensions
{
    public static async Task<PaginatedResult<T>> ToPaginatedAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize)
    {
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return PaginatedResult<T>.Create(items, totalCount, pageNumber, pageSize);
    }

    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        string? sortDir,
        string[] allowedFields,
        string defaultField,
        bool defaultDescending = false)
    {
        PropertyInfo? property = null;
        var descending = defaultDescending;

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var candidate = typeof(T).GetProperty(
                sortBy,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (candidate is not null && allowedFields.Contains(candidate.Name, StringComparer.OrdinalIgnoreCase))
            {
                property = candidate;
                descending = (sortDir ?? "").Equals("desc", StringComparison.OrdinalIgnoreCase);
            }
        }

        property ??= typeof(T).GetProperty(
            defaultField,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (property is null)
            return query;

        return descending
            ? query.OrderByDescending(e => EF.Property<object>(e!, property.Name))
            : query.OrderBy(e => EF.Property<object>(e!, property.Name));
    }
}
