using Coworking.Application.Common.Pagination;

namespace Coworking.Application.Rooms.Dtos;

/// <summary>
/// Query string of <c>GET /api/rooms</c>, e.g. <c>?page=1&amp;pageSize=20&amp;search=office&amp;isActive=true</c>.
/// Every filter is optional; leaving it out means "don't filter by this".
/// </summary>
public sealed class ListRoomsQuery : PageQuery
{
    /// <summary>Part of the room name (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>true = only active rooms, false = only deactivated ones.</summary>
    public bool? IsActive { get; set; }
}
