using System.Linq.Expressions;
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
        var isDescending = !string.IsNullOrWhiteSpace(sortDir)
            ? sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase)
            : defaultDescending;

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            var candidate = typeof(T).GetProperty(
                sortBy,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (candidate is not null && allowedFields.Contains(candidate.Name, StringComparer.OrdinalIgnoreCase))
            {
                property = candidate;
            }
        }

        property ??= typeof(T).GetProperty(
            defaultField,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (property is null)
            return query;

        try
        {
            var param = Expression.Parameter(typeof(T), "e");
            var propAccess = Expression.Property(param, property);
            var keySelector = Expression.Lambda(propAccess, param);
            var methodName = isDescending ? "OrderByDescending" : "OrderBy";
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), property.PropertyType);

            return (IQueryable<T>)method.Invoke(null, new object[] { query, keySelector })!;
        }
        catch
        {
            return isDescending
                ? query.OrderByDescending(e => EF.Property<object>(e!, property.Name))
                : query.OrderBy(e => EF.Property<object>(e!, property.Name));
        }
    }
}
