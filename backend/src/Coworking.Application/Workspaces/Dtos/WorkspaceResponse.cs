using Coworking.Domain.Enums;

namespace Coworking.Application.Workspaces.Dtos;

/// <summary>A workspace as shown in the manager's list and edit form (FR09).</summary>
/// <param name="Id">Workspace id.</param>
/// <param name="Code">Unique label in upper case, e.g. "OS-01".</param>
/// <param name="Type">HotDesk, FixedDesk, Office or MeetingRoom (sent as a string).</param>
/// <param name="Capacity">Number of people.</param>
/// <param name="IsActive">The workspace's own flag. It is offered only if the room is active too.</param>
/// <param name="RoomId">Id of the room it is in.</param>
/// <param name="RoomName">Name of that room (saves the UI a second request).</param>
/// <param name="RoomIsActive">Whether that room is active.</param>
/// <param name="IsAssigned">Only for fixed desks: a member has this desk assigned (then it can't be deactivated, deleted or retyped).</param>
public sealed record WorkspaceResponse(
    int Id,
    string Code,
    WorkspaceType Type,
    int Capacity,
    bool IsActive,
    int RoomId,
    string RoomName,
    bool RoomIsActive,
    bool IsAssigned
);
