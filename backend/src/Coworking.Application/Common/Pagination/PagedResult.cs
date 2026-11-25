namespace Coworking.Application.Common.Pagination;

/// <summary>
/// One page of a list plus what the frontend needs to draw the pager.
/// </summary>
/// <typeparam name="T">Item type, e.g. EmployeeResponse.</typeparam>
/// <param name="Items">The items on this page (may be empty).</param>
/// <param name="Page">This page's number (1-based).</param>
/// <param name="PageSize">Requested items per page.</param>
/// <param name="TotalCount">Number of items on ALL pages that match the filters.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Number of pages (0 when nothing matches). Rounds up: 41 items / 20 = 3 pages.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
}
