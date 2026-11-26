using Coworking.Application.Common.Pagination;
using Coworking.Domain.Enums;

namespace Coworking.Application.Workspaces.Dtos;

/// <summary>
/// Query string of <c>GET /api/workspaces</c>, e.g.
/// <c>?roomId=3&amp;type=HotDesk&amp;isActive=true&amp;search=os-&amp;page=1&amp;pageSize=50</c>.
/// Every filter is optional.
/// </summary>
public sealed class ListWorkspacesQuery : PageQuery
{
    /// <summary>Only workspaces in this room (the room detail page uses it).</summary>
    public int? RoomId { get; set; }

    /// <summary>Only workspaces of this type.</summary>
    public WorkspaceType? Type { get; set; }

    /// <summary>true = only active, false = only deactivated (the workspace's own flag).</summary>
    public bool? IsActive { get; set; }

    /// <summary>Part of the code, letter case ignored.</summary>
    public string? Search { get; set; }
}
