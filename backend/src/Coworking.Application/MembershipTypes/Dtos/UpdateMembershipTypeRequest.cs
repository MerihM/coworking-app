namespace Coworking.Application.MembershipTypes.Dtos;

/// <summary>
/// Body of <c>PUT /api/membership-types/{id}</c>: the whole editable state. A new price or duration
/// applies only to memberships bought FROM NOW ON – bought ones keep their own copy (class diagram:
/// Clanarina.cijena). The active flag has its own endpoints.
/// </summary>
/// <param name="Name">New name (unique, letter case ignored).</param>
/// <param name="DurationDays">New duration, 1–365 days.</param>
/// <param name="Price">New price, 0.01–10 000.00 KM.</param>
public sealed record UpdateMembershipTypeRequest(string Name, int DurationDays, decimal Price)
    : IMembershipTypeInput;
