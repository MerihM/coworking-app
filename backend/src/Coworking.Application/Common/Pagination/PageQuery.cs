namespace Coworking.Application.Common.Pagination;

/// <summary>
/// Paging parameters shared by every list endpoint (<c>?page=2&amp;pageSize=20</c>).
/// List queries inherit from it and add their own filters.
/// </summary>
/// <remarks>
/// A class with setters (not a record) because MVC binds query strings by setting properties.
/// The defaults apply when the client leaves a parameter out. Limits are checked by
/// <see cref="PageQueryValidator{T}"/>.
/// </remarks>
public class PageQuery
{
    /// <summary>Largest allowed page size; protects the database from "give me everything".</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page number, starting at 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Items per page (1–100, default 20).</summary>
    public int PageSize { get; set; } = 20;
}
