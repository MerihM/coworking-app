namespace Coworking.Application.Memberships.Dtos;

/// <summary>
/// Body of <c>POST /api/memberships/mine</c> (the member buys, UC03 step 3) and
/// <c>POST /api/members/{id}/memberships</c> (reception sells at the desk).
/// </summary>
/// <param name="MembershipTypeId">An active membership type from the offer.</param>
public sealed record BuyMembershipRequest(int MembershipTypeId);
