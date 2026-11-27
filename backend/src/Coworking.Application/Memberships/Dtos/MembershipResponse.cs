using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.Application.Memberships.Dtos;

/// <summary>A purchased membership with its payment (FR03, FR07; member's history and reception lists).</summary>
/// <param name="Id">Membership id.</param>
/// <param name="MemberId">Member id.</param>
/// <param name="MemberName">"First Last".</param>
/// <param name="MemberEmail">Member's e-mail.</param>
/// <param name="MembershipTypeId">Type it was bought from.</param>
/// <param name="TypeName">Type name, e.g. "Monthly".</param>
/// <param name="DurationDays">Duration copied at purchase.</param>
/// <param name="Price">Price copied at purchase (KM) – the amount to pay at the desk.</param>
/// <param name="Status">Unpaid, Active or Expired as of today (derived, see Membership.EffectiveStatusOn).</param>
/// <param name="PurchasedAt">Purchase moment (UTC).</param>
/// <param name="StartDate">First valid day; null until paid (BR05).</param>
/// <param name="EndDate">First day NO LONGER valid (exclusive); null until paid.</param>
/// <param name="Payment">The recorded payment, or null while unpaid.</param>
public sealed record MembershipResponse(
    int Id,
    int MemberId,
    string MemberName,
    string MemberEmail,
    int MembershipTypeId,
    string TypeName,
    int DurationDays,
    decimal Price,
    MembershipStatus Status,
    DateTimeOffset PurchasedAt,
    DateOnly? StartDate,
    DateOnly? EndDate,
    PaymentInfo? Payment
)
{
    /// <summary>Last valid day (EndDate − 1), what the UI shows; null until paid.</summary>
    public DateOnly? LastDay => EndDate?.AddDays(-1);

    /// <summary>Maps a membership loaded with its member, type and payment (incl. the employee).</summary>
    /// <param name="membership">The membership.</param>
    /// <param name="today">Current Sarajevo date (for the Expired status).</param>
    /// <returns>The response.</returns>
    public static MembershipResponse From(Membership membership, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new MembershipResponse(
            membership.Id,
            membership.MemberId,
            $"{membership.Member.FirstName} {membership.Member.LastName}",
            membership.Member.Email,
            membership.MembershipTypeId,
            membership.MembershipType.Name,
            membership.DurationDays,
            membership.Price,
            membership.EffectiveStatusOn(today),
            membership.PurchasedAt,
            membership.StartDate,
            membership.EndDate,
            membership.Payment is { } payment
                ? new PaymentInfo(
                    payment.PaidAt,
                    payment.Amount,
                    payment.Method,
                    $"{payment.Employee.FirstName} {payment.Employee.LastName}"
                )
                : null
        );
    }
}

/// <summary>A recorded payment (Uplata).</summary>
/// <param name="PaidAt">Moment of payment (UTC).</param>
/// <param name="Amount">Amount in KM.</param>
/// <param name="Method">Cash or Card.</param>
/// <param name="RecordedByName">Employee who recorded it ("First Last").</param>
public sealed record PaymentInfo(DateTimeOffset PaidAt, decimal Amount, PaymentMethod Method, string RecordedByName);
