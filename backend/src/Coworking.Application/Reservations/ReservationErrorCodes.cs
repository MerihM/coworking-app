namespace Coworking.Application.Reservations;

/// <summary>
/// Error codes of the reservation use cases (FR04, FR05, FR06). Sent as the <c>code</c> field of
/// ProblemDetails; the frontend translates them. Never change an existing value.
/// </summary>
/// <remarks>
/// The design-document rules BR02 (no active membership), BR03 (member cancels too late) and BR04
/// (working hours) are NOT codes here: the domain refuses them with a <c>ruleCode</c> (422), and the
/// frontend maps that field. BR01 has both: <c>code</c> = <see cref="SlotTaken"/> and <c>ruleCode</c> = "BR01".
/// </remarks>
public static class ReservationErrorCodes
{
    /// <summary>422 – the workspace or its room is deactivated, or it is a fixed desk (assigned, not booked).</summary>
    public const string WorkspaceUnavailable = "reservations.workspace_unavailable";

    /// <summary>422 – the reservation would start now or in the past.</summary>
    public const string InPast = "reservations.in_past";

    /// <summary>
    /// 409 – BR01: the workspace already has a confirmed reservation that overlaps the period
    /// (service check, or the EXCLUDE constraint when two requests arrive at the same moment).
    /// The UI then offers alternatives (UC06).
    /// </summary>
    public const string SlotTaken = "reservations.slot_taken";

    /// <summary>409 – the reservation is already cancelled or has already ended.</summary>
    public const string NotCancellable = "reservations.not_cancellable";
}
