namespace Coworking.Application.Rooms;

/// <summary>
/// Error codes for room management (FR09). Sent as the <c>code</c> field of ProblemDetails; the
/// frontend translates them. Never change an existing value.
/// </summary>
public static class RoomErrorCodes
{
    /// <summary>
    /// 409 – another room already has this name (letter case ignored). The frontend shows it as a
    /// field error under "Name". Deleting a room that still has workspaces uses the shared
    /// <c>conflict.in_use</c> code instead (it comes from the database's foreign key).
    /// </summary>
    public const string NameTaken = "rooms.name_taken";
}
