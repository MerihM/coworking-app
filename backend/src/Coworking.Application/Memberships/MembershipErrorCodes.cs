namespace Coworking.Application.Memberships;

/// <summary>
/// Error codes for buying and paying memberships (FR03, FR07). Sent as the <c>code</c> field of
/// ProblemDetails; the frontend translates them. Never change an existing value.
/// </summary>
/// <remarks>
/// A payment amount that differs from the price is NOT here: the domain refuses it with rule
/// code BR05 (422, field <c>ruleCode</c>), like the other design-document rules.
/// </remarks>
public static class MembershipErrorCodes
{
    /// <summary>422 – the chosen membership type is no longer offered (deactivated by the manager).</summary>
    public const string TypeInactive = "memberships.type_inactive";

    /// <summary>
    /// 409 – the member already has an unpaid membership. One open order at a time keeps the
    /// reception's "waiting for payment" list clean; the member can cancel it and choose again.
    /// </summary>
    public const string UnpaidExists = "memberships.unpaid_exists";

    /// <summary>409 when paying a membership twice; 422 when deleting a paid one (history, revenue).</summary>
    public const string AlreadyPaid = "memberships.already_paid";
}
