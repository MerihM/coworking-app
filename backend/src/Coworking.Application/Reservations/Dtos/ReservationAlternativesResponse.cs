using Coworking.Domain.Enums;

namespace Coworking.Application.Reservations.Dtos;

/// <summary>
/// UC06 "Ponudi alternativni termin": what the member can take instead of the occupied slot.
/// Accepting an offer = making a normal reservation with it (POST /api/reservations/mine).
/// </summary>
/// <param name="OtherWorkspaces">
/// Up to 5 free workspaces of the same type (and at least the same capacity) at the SAME time.
/// Workspaces in the same room come first.
/// </param>
/// <param name="NextSlots">
/// Up to 3 free slots of the SAME workspace with the same duration: later that day first, then the
/// following days (within a week), always inside working hours (BR04).
/// </param>
public sealed record ReservationAlternativesResponse(
    IReadOnlyList<AlternativeWorkspace> OtherWorkspaces,
    IReadOnlyList<AlternativeSlot> NextSlots
);

/// <summary>Another free workspace for the requested time.</summary>
/// <param name="Id">Workspace id.</param>
/// <param name="Code">Label, e.g. "OS-02".</param>
/// <param name="Type">Same type as the requested one.</param>
/// <param name="Capacity">Number of people.</param>
/// <param name="RoomId">Room id.</param>
/// <param name="RoomName">Room name.</param>
public sealed record AlternativeWorkspace(
    int Id,
    string Code,
    WorkspaceType Type,
    int Capacity,
    int RoomId,
    string RoomName
);

/// <summary>A free period of the requested workspace.</summary>
/// <param name="Date">Local date.</param>
/// <param name="From">Local start.</param>
/// <param name="To">Local end.</param>
/// <param name="StartsAt">Start (UTC).</param>
/// <param name="EndsAt">End (UTC).</param>
public sealed record AlternativeSlot(DateOnly Date, TimeOnly From, TimeOnly To, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
