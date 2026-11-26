namespace Coworking.Application.Workspaces;

/// <summary>
/// Error codes for workspace management (FR09). Sent as the <c>code</c> field of ProblemDetails;
/// the frontend translates them. Never change an existing value.
/// </summary>
public static class WorkspaceErrorCodes
{
    /// <summary>409 – another workspace already uses this code. Shown as a field error under "Code".</summary>
    public const string CodeTaken = "workspaces.code_taken";

    /// <summary>
    /// 422 – the workspace is a fixed desk assigned to a member, so it can't be deactivated,
    /// deleted or turned into another type. Remove the assignment first (member management, Sprint 4).
    /// Without this rule a delete would silently unassign the member (the FK is ON DELETE SET NULL).
    /// </summary>
    public const string FixedAssigned = "workspaces.fixed_assigned";

    /// <summary>422 – workspaces can't be created in, or moved into, a deactivated room.</summary>
    public const string RoomInactive = "workspaces.room_inactive";
}
