namespace Coworking.Application.Members.Dtos;

/// <summary>
/// A fixed desk reception can assign to a member (CW-28): the choices for the "Fixed desk" field
/// of the member form. Reception can't use /api/workspaces (manager only), so this is the narrow
/// read-only view it needs.
/// </summary>
/// <param name="Id">Workspace id.</param>
/// <param name="Code">Workspace code, e.g. "OS-25".</param>
/// <param name="RoomName">Room the desk is in.</param>
/// <param name="AssignedMemberId">The member who has it, or null when it is free.</param>
/// <param name="AssignedMemberName">"First Last" of that member, or null.</param>
public sealed record FixedDeskOption(
    int Id,
    string Code,
    string RoomName,
    int? AssignedMemberId,
    string? AssignedMemberName
);
