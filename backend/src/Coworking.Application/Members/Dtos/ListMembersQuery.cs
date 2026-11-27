using Coworking.Application.Common.Pagination;

namespace Coworking.Application.Members.Dtos;

/// <summary>
/// Query string of <c>GET /api/members</c>, e.g. <c>?search=hodzic&amp;isActive=true&amp;page=1</c>.
/// Every filter is optional.
/// </summary>
public sealed class ListMembersQuery : PageQuery
{
    /// <summary>Part of the first name, last name, full name, e-mail or phone (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>true = only active, false = only deactivated.</summary>
    public bool? IsActive { get; set; }
}
