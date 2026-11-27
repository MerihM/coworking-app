using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.Application.Members.Dtos;

/// <summary>
/// A member as reception sees it in the list and on the detail page (FR08). Never contains the
/// password hash.
/// </summary>
/// <param name="Id">Member id.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Email">Login e-mail (lower case).</param>
/// <param name="Phone">Phone number.</param>
/// <param name="IsActive">Deactivated members can't log in or get new memberships.</param>
/// <param name="RegisteredAt">Registration moment (UTC).</param>
/// <param name="FixedDesk">The assigned fixed desk, or null.</param>
/// <param name="CurrentMembership">The membership valid today, or null (no valid membership = no reservations, BR02).</param>
/// <param name="ValidThrough">
/// Last valid day counting paid renewals that start later (BR05), or null. E.g. a monthly
/// membership plus a paid renewal → the renewal's last day.
/// </param>
/// <param name="HasUnpaidMembership">An unpaid membership is waiting for payment at the desk.</param>
public sealed record MemberResponse(
    int Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    bool IsActive,
    DateTimeOffset RegisteredAt,
    FixedDeskInfo? FixedDesk,
    CurrentMembershipInfo? CurrentMembership,
    DateOnly? ValidThrough,
    bool HasUnpaidMembership
)
{
    /// <summary>
    /// Maps a member loaded WITH its fixed desk and its memberships (incl. their types).
    /// Mapping in memory (not in SQL) lets us reuse the domain rule <see cref="Membership.IsActiveOn"/>.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <param name="today">Current Sarajevo date.</param>
    /// <returns>The response.</returns>
    public static MemberResponse From(Member member, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(member);

        var current = member
            .Memberships.Where(ms => ms.IsActiveOn(today))
            .OrderBy(ms => ms.StartDate)
            .FirstOrDefault();

        // EndDate is exclusive (ADR 0002): the last valid day is the day before it.
        var paidUntil = member
            .Memberships.Where(ms => ms.Status == MembershipStatus.Active && ms.EndDate > today)
            .Max(ms => ms.EndDate);

        return new MemberResponse(
            member.Id,
            member.FirstName,
            member.LastName,
            member.Email,
            member.Phone,
            member.IsActive,
            member.RegisteredAt,
            member.FixedWorkspace is { } desk ? new FixedDeskInfo(desk.Id, desk.Code) : null,
            current is null
                ? null
                : new CurrentMembershipInfo(
                    current.Id,
                    current.MembershipType.Name,
                    current.StartDate!.Value,
                    current.EndDate!.Value
                ),
            paidUntil?.AddDays(-1),
            member.Memberships.Any(ms => ms.Status == MembershipStatus.Unpaid)
        );
    }
}

/// <summary>The fixed desk assigned to a member.</summary>
/// <param name="Id">Workspace id.</param>
/// <param name="Code">Workspace code, e.g. "OS-25".</param>
public sealed record FixedDeskInfo(int Id, string Code);

/// <summary>The membership a member can use today.</summary>
/// <param name="Id">Membership id.</param>
/// <param name="TypeName">Membership type name, e.g. "Monthly".</param>
/// <param name="StartDate">First valid day.</param>
/// <param name="EndDate">First day NO LONGER valid (exclusive, ADR 0002).</param>
public sealed record CurrentMembershipInfo(int Id, string TypeName, DateOnly StartDate, DateOnly EndDate)
{
    /// <summary>Last valid day (EndDate − 1) – what the UI shows as "valid until".</summary>
    public DateOnly LastDay => EndDate.AddDays(-1);
}
