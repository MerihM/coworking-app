using Coworking.Application.Common.Pagination;

namespace Coworking.Application.MembershipTypes.Dtos;

/// <summary>
/// Query string of <c>GET /api/membership-types</c>, e.g. <c>?search=month&amp;isActive=true&amp;page=1</c>.
/// Every filter is optional.
/// </summary>
public sealed class ListMembershipTypesQuery : PageQuery
{
    /// <summary>Part of the name (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>true = only offered types, false = only deactivated ones.</summary>
    public bool? IsActive { get; set; }
}
