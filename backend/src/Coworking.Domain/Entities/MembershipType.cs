using Coworking.Domain.Common;

namespace Coworking.Domain.Entities;

/// <summary>
/// An offer from the price list, e.g. "Daily", "Monthly" (design document: TipClanarine).
/// Defined by the manager (FR10, UC12).
/// </summary>
public class MembershipType : Entity
{
    /// <summary>Used by EF Core when loading from the database.</summary>
    private MembershipType() { }

    /// <summary>Creates a new, active membership type.</summary>
    /// <param name="name">Display name, e.g. "Monthly".</param>
    /// <param name="durationDays">How many days a membership of this type lasts (must be &gt; 0).</param>
    /// <param name="price">Current price in KM (must be ≥ 0).</param>
    public MembershipType(string name, int durationDays, decimal price)
    {
        Update(name, durationDays, price);
        IsActive = true;
    }

    /// <summary>Display name (naziv).</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Duration in days (trajanjeDana).</summary>
    public int DurationDays { get; private set; }

    /// <summary>Current price in KM (cijena). Already purchased memberships keep their own copy.</summary>
    public decimal Price { get; private set; }

    /// <summary>Inactive types are no longer offered but stay in the database for history (aktivan).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Changes name, duration and price. Does not affect memberships that were already purchased.</summary>
    /// <param name="name">New name.</param>
    /// <param name="durationDays">New duration in days.</param>
    /// <param name="price">New price in KM.</param>
    public void Update(string name, int durationDays, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(durationDays);
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        Name = name.Trim();
        DurationDays = durationDays;
        Price = price;
    }

    /// <summary>Stops offering this type (FR10). Existing memberships stay valid.</summary>
    public void Deactivate() => IsActive = false;

    /// <summary>Offers this type again.</summary>
    public void Reactivate() => IsActive = true;
}
