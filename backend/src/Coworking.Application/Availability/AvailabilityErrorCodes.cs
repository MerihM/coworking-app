namespace Coworking.Application.Availability;

/// <summary>
/// Error codes of the availability search (FR02). Sent as the <c>code</c> field of ProblemDetails;
/// the frontend translates them. Never change an existing value.
/// </summary>
/// <remarks>
/// Outside working hours is NOT a code here: it comes from the domain rule as
/// <c>ruleCode: "BR04"</c> (the same check reservations use), and the frontend maps that.
/// </remarks>
public static class AvailabilityErrorCodes
{
    /// <summary>422 – the requested period has already ended; nothing can be booked in the past.</summary>
    public const string InPast = "availability.in_past";
}
