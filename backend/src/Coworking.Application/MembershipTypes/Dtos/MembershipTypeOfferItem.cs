namespace Coworking.Application.MembershipTypes.Dtos;

/// <summary>
/// One entry of the public price list (<c>GET /api/membership-types/offer</c>, UC03 step 2):
/// only what a visitor needs to see, no status or usage numbers.
/// </summary>
/// <param name="Id">Membership type id (the member picks it when buying, Sprint 4).</param>
/// <param name="Name">Name, e.g. "Monthly".</param>
/// <param name="DurationDays">Duration in days.</param>
/// <param name="Price">Price in KM (BAM).</param>
public sealed record MembershipTypeOfferItem(int Id, string Name, int DurationDays, decimal Price);
