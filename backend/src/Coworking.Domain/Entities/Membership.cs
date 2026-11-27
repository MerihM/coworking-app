using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Domain.Entities;

/// <summary>
/// A membership bought by a specific member (design document: Clanarina).
/// Keeps its own price and duration from the moment of purchase, so later price-list
/// changes don't affect it (chapter 4.2).
/// </summary>
/// <remarks>
/// Validity period: from <see cref="StartDate"/> (inclusive) to <see cref="EndDate"/> (EXCLUSIVE).
/// EndDate = StartDate + DurationDays (TC07) is the first day the membership is no longer valid,
/// and the day a renewal starts (BR05), so there is no gap and no overlap. See ADR 0002.
/// </remarks>
public class Membership : Entity
{
    /// <summary>Used by EF Core when loading from the database.</summary>
    private Membership() { }

    /// <summary>Created only through <see cref="Member.PurchaseMembership"/>.</summary>
    internal Membership(Member member, MembershipType type, DateTimeOffset purchasedAt)
    {
        Member = member;
        MembershipType = type;
        PurchasedAt = purchasedAt.ToUniversalTime();
        Price = type.Price;
        DurationDays = type.DurationDays;
        Status = MembershipStatus.Unpaid;
    }

    /// <summary>Foreign key of the member.</summary>
    public int MemberId { get; private set; }

    /// <summary>The member who bought it.</summary>
    public Member Member { get; private set; } = null!;

    /// <summary>Foreign key of the membership type.</summary>
    public int MembershipTypeId { get; private set; }

    /// <summary>The type it was bought from.</summary>
    public MembershipType MembershipType { get; private set; } = null!;

    /// <summary>Purchase moment in UTC (datumKupovine).</summary>
    public DateTimeOffset PurchasedAt { get; private set; }

    /// <summary>First valid day (datumPocetka); null until paid.</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>First day NO LONGER valid (datumIsteka, exclusive); null until paid.</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>Price at the moment of purchase, in KM (cijena).</summary>
    public decimal Price { get; private set; }

    /// <summary>Duration at the moment of purchase, in days.</summary>
    public int DurationDays { get; private set; }

    /// <summary>Unpaid → Active (after payment) → Expired.</summary>
    public MembershipStatus Status { get; private set; }

    /// <summary>The payment, if recorded (Clanarina 1 – 0..1 Uplata).</summary>
    public Payment? Payment { get; private set; }

    /// <summary>
    /// Is the membership valid on this date? (diagram: jeAktivna(datum), used by BR02).
    /// Unpaid and expired memberships are never active.
    /// </summary>
    /// <param name="date">Local date to check.</param>
    public bool IsActiveOn(DateOnly date) =>
        Status == MembershipStatus.Active && StartDate <= date && date < EndDate;

    /// <summary>
    /// The status as the user should see it on <paramref name="today"/>: a paid membership whose
    /// period is over is shown as Expired even while the stored status is still Active.
    /// </summary>
    /// <remarks>
    /// Why derived instead of stored (CW-26): nothing runs at midnight to flip the status, and a
    /// background job would be one more moving part. BR02 never depended on the stored value anyway
    /// – <see cref="IsActiveOn"/> checks the dates – so deriving it on read is always correct.
    /// </remarks>
    /// <param name="today">Current local (Sarajevo) date.</param>
    /// <returns>Unpaid, Active (also for a paid renewal that starts later) or Expired.</returns>
    public MembershipStatus EffectiveStatusOn(DateOnly today) =>
        Status == MembershipStatus.Active && EndDate <= today ? MembershipStatus.Expired : Status;

    /// <summary>Marks an active membership as expired once its period is over (for overviews and reports).</summary>
    /// <param name="today">Current local date.</param>
    public void ExpireIfEnded(DateOnly today)
    {
        if (Status == MembershipStatus.Active && today >= EndDate)
        {
            Status = MembershipStatus.Expired;
        }
    }

    /// <summary>
    /// Records the payment and activates the membership (BR05). Called by
    /// <see cref="Employee.RecordPayment"/>, which also calculates the start date.
    /// </summary>
    internal Payment Pay(
        Employee recordedBy,
        decimal amount,
        PaymentMethod method,
        DateTimeOffset paidAt,
        DateOnly startDate
    )
    {
        if (Status != MembershipStatus.Unpaid)
        {
            throw new DomainException("This membership has already been paid.", "BR05");
        }

        if (amount != Price)
        {
            throw new DomainException(
                $"The payment amount must equal the membership price ({Price:0.00} KM).",
                "BR05"
            );
        }

        Payment = new Payment(this, recordedBy, amount, method, paidAt);
        Activate(startDate);
        return Payment;
    }

    /// <summary>
    /// Sets the validity period and the Active status (diagram: aktiviraj(datumPocetka)).
    /// Private on purpose: the ONLY way to activate a membership is a payment (BR05).
    /// </summary>
    private void Activate(DateOnly startDate)
    {
        StartDate = startDate;
        EndDate = startDate.AddDays(DurationDays);
        Status = MembershipStatus.Active;
    }
}
