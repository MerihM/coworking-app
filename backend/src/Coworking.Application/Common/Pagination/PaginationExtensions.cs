using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Common.Pagination;

/// <summary>Turns any EF Core query into one page of results.</summary>
public static class PaginationExtensions
{
    /// <summary>
    /// Runs two SQL queries: <c>SELECT count(*)</c> for the total, and the page itself with
    /// <c>LIMIT pageSize OFFSET (page − 1) × pageSize</c>.
    /// </summary>
    /// <remarks>
    /// The query MUST be ordered before calling this. Without ORDER BY, PostgreSQL may return rows
    /// in a different order each time, and the same row could appear on two pages.
    /// </remarks>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="query">An ordered query (projection to the response type already applied).</param>
    /// <param name="paging">Page and page size (already validated).</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns>The page.</returns>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PageQuery paging,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(paging);

        // EF drops the ORDER BY for the count query, so this stays cheap.
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, paging.Page, paging.PageSize, totalCount);
    }
}
