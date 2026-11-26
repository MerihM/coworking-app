namespace Coworking.Application.MembershipTypes;

/// <summary>
/// Error codes for membership type management (FR10). Sent as the <c>code</c> field of
/// ProblemDetails; the frontend translates them. Never change an existing value.
/// </summary>
public static class MembershipTypeErrorCodes
{
    /// <summary>
    /// 409 – another membership type already has this name (letter case ignored). Shown as a field
    /// error under "Name". Deleting a type that was ever bought uses the shared <c>conflict.in_use</c>.
    /// </summary>
    public const string NameTaken = "membership_types.name_taken";
}
