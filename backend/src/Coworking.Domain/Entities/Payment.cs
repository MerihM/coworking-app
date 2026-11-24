using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// A recorded payment for a membership (design document: Uplata). A separate class because it
/// has its own data: time, amount, method and the employee who recorded it (chapter 4.1).
/// Used by the revenue report (FR11).
/// </summary>
public class Payment : Entity
{
    /// <summary>Used by EF Core when loading from the database.</summary>
    private Payment() { }

    /// <summary>Created only by <see cref="Membership"/> when it is paid, so every payment has a membership.</summary>
    internal Payment(
        Membership membership,
        Employee recordedBy,
        decimal amount,
        PaymentMethod method,
        DateTimeOffset paidAt
    )
    {
        Membership = membership;
        Employee = recordedBy;
        Amount = amount;
        Method = method;
        PaidAt = paidAt.ToUniversalTime();
    }

    /// <summary>Foreign key of the membership. Unique: one payment per membership (CW-7).</summary>
    public int MembershipId { get; private set; }

    /// <summary>The paid membership.</summary>
    public Membership Membership { get; private set; } = null!;

    /// <summary>Foreign key of the employee who recorded the payment.</summary>
    public int EmployeeId { get; private set; }

    /// <summary>The employee who recorded the payment (Zaposlenik 1 – 0..* Uplata).</summary>
    public Employee Employee { get; private set; } = null!;

    /// <summary>Moment of payment in UTC (datumUplate).</summary>
    public DateTimeOffset PaidAt { get; private set; }

    /// <summary>Amount in KM (iznos).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Cash or card (nacinPlacanja).</summary>
    public PaymentMethod Method { get; private set; }
}
