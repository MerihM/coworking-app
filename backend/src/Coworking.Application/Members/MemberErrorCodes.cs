namespace Coworking.Application.Members;

/// <summary>
/// Error codes for member management (FR08, UC10). Sent as the <c>code</c> field of
/// ProblemDetails; the frontend translates them. Never change an existing value.
/// </summary>
public static class MemberErrorCodes
{
    /// <summary>422 – the member account is deactivated, so it can't get a new membership.</summary>
    public const string Inactive = "members.inactive";

    /// <summary>
    /// 422 – the workspace chosen as the member's fixed desk is not an active FixedDesk in an
    /// active room (hot desks, offices and meeting rooms are booked by the hour, never assigned).
    /// </summary>
    public const string FixedDeskInvalid = "members.fixed_desk_invalid";

    /// <summary>409 – the fixed desk is already assigned to another member (Clan – RadnoMjesto 0..1 – 0..1).</summary>
    public const string FixedDeskTaken = "members.fixed_desk_taken";
}
