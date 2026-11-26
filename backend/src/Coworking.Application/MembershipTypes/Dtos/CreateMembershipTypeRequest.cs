namespace Coworking.Application.MembershipTypes.Dtos;

/// <summary>Body of <c>POST /api/membership-types</c>: a new, active type (FR10, UC12).</summary>
/// <param name="Name">Name, unique (letter case ignored), 2–100 characters.</param>
/// <param name="DurationDays">1–365 days (1 = daily, 7 = weekly, 30 = monthly).</param>
/// <param name="Price">0.01–10 000.00 KM, at most two decimals. Must be a JSON number, not a string.</param>
public sealed record CreateMembershipTypeRequest(string Name, int DurationDays, decimal Price)
    : IMembershipTypeInput;
