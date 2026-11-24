namespace Coworking.Domain.Enums;

/// <summary>Status of a purchased membership (design document: StatusClanarine).</summary>
public enum MembershipStatus
{
    /// <summary>Purchased but not paid yet; does not allow reservations (Neplacena, BR05).</summary>
    Unpaid,

    /// <summary>Paid; valid from StartDate until the day before EndDate (Aktivna).</summary>
    Active,

    /// <summary>The validity period has passed (Istekla).</summary>
    Expired,
}
