namespace Coworking.Application.MembershipTypes.Dtos;

/// <summary>A membership type as shown in the manager's list and edit form (FR10).</summary>
/// <param name="Id">Membership type id.</param>
/// <param name="Name">Name, e.g. "Monthly".</param>
/// <param name="DurationDays">How many days a membership of this type lasts.</param>
/// <param name="Price">Current price in KM (BAM), two decimals.</param>
/// <param name="IsActive">Inactive types are not offered any more; bought memberships stay valid.</param>
/// <param name="MembershipCount">
/// How many memberships were bought with this type. Greater than 0 = it can't be deleted, only
/// deactivated (the UI can disable the delete action up front).
/// </param>
public sealed record MembershipTypeResponse(
    int Id,
    string Name,
    int DurationDays,
    decimal Price,
    bool IsActive,
    int MembershipCount
);
