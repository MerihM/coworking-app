using Coworking.Domain.Enums;

namespace Coworking.Application.Availability.Dtos;

/// <summary>Result of an availability search (FR02, UC02): free workspaces grouped by room.</summary>
/// <param name="Date">The searched local date (echo of the query).</param>
/// <param name="From">The searched local start time.</param>
/// <param name="To">The searched local end time.</param>
/// <param name="StartsAt">The same start as a UTC instant – what a reservation will store (Sprint 5).</param>
/// <param name="EndsAt">The same end as a UTC instant.</param>
/// <param name="TotalFree">Number of free workspaces over all rooms.</param>
/// <param name="Rooms">Rooms that have at least one free workspace, sorted by floor and name.</param>
public sealed record AvailabilityResponse(
    DateOnly Date,
    TimeOnly From,
    TimeOnly To,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int TotalFree,
    IReadOnlyList<AvailableRoom> Rooms
);

/// <summary>A room with its free workspaces.</summary>
/// <param name="Id">Room id.</param>
/// <param name="Name">Room name.</param>
/// <param name="Floor">Floor.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Workspaces">Free workspaces in this room, sorted by code.</param>
public sealed record AvailableRoom(
    int Id,
    string Name,
    int Floor,
    string? Description,
    IReadOnlyList<AvailableWorkspace> Workspaces
);

/// <summary>One free workspace.</summary>
/// <param name="Id">Workspace id (Sprint 5: the "Reserve" button sends it).</param>
/// <param name="Code">Label, e.g. "OS-01".</param>
/// <param name="Type">HotDesk, Office or MeetingRoom.</param>
/// <param name="Capacity">Number of people.</param>
public sealed record AvailableWorkspace(int Id, string Code, WorkspaceType Type, int Capacity);
