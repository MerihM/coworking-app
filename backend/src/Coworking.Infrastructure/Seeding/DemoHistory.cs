using Coworking.Domain.Common;
using Coworking.Domain.Enums;

namespace Coworking.Infrastructure.Seeding;

/// <summary>A membership a history member buys and pays on <paramref name="Day"/>.</summary>
/// <param name="Email">The member.</param>
/// <param name="MembershipTypeName">Membership type.</param>
/// <param name="Method">Payment method.</param>
/// <param name="Day">Local day of purchase and payment (= start of the membership, BR05).</param>
public sealed record PlannedPurchase(string Email, string MembershipTypeName, PaymentMethod Method, DateOnly Day);

/// <summary>A past reservation of a history member.</summary>
/// <param name="Email">The member.</param>
/// <param name="WorkspaceCode">Workspace.</param>
/// <param name="Day">Local day.</param>
/// <param name="From">Local start time.</param>
/// <param name="To">Local end time.</param>
/// <param name="Cancelled">The member cancelled it in the morning.</param>
public sealed record PlannedVisit(string Email, string WorkspaceCode, DateOnly Day, TimeOnly From, TimeOnly To, bool Cancelled);

/// <summary>Everything the history step will insert.</summary>
/// <param name="Purchases">Paid memberships, oldest first.</param>
/// <param name="Visits">Reservations, oldest first.</param>
public sealed record DemoHistoryPlan(IReadOnlyList<PlannedPurchase> Purchases, IReadOnlyList<PlannedVisit> Visits);

/// <summary>
/// Turns <see cref="DemoData.HistoryMembers"/> into concrete days (CW-32). Pure – no database, no
/// clock – so a test can check the plan (no Sundays, no future days, every visit covered by a paid
/// membership) and the seeder only has to carry it out.
/// </summary>
public static class DemoHistory
{
    /// <summary>
    /// Plans the <see cref="DemoData.HistoryDays"/> days before <paramref name="today"/> (today itself
    /// is NOT included, so nothing collides with the upcoming demo reservations).
    /// </summary>
    /// <param name="today">Today's local date.</param>
    /// <param name="durationDaysByType">Duration of each membership type by name (from the database).</param>
    /// <returns>The purchases and visits.</returns>
    /// <remarks>
    /// A member buys a new membership on the first day they want to come and have none: a Monthly
    /// member pays once, a Weekly member every 7 days, a Daily member on every visit day – the same
    /// thing a real member would do because of BR02. A member whose type is missing is skipped.
    /// </remarks>
    public static DemoHistoryPlan Plan(DateOnly today, IReadOnlyDictionary<string, int> durationDaysByType)
    {
        ArgumentNullException.ThrowIfNull(durationDaysByType);

        var first = today.AddDays(-DemoData.HistoryDays);
        var purchases = new List<PlannedPurchase>();
        var visits = new List<PlannedVisit>();

        foreach (var member in DemoData.HistoryMembers)
        {
            if (!durationDaysByType.TryGetValue(member.MembershipTypeName, out var duration))
            {
                continue;
            }

            DateOnly? paidUntil = null; // exclusive, like Membership.EndDate

            for (var day = first; day < today; day = day.AddDays(1))
            {
                var todays = member.Visits.Where(v => v.Day == day.DayOfWeek).ToList();
                if (todays.Count == 0 || WorkingHours.GetHours(day.DayOfWeek) is null)
                {
                    continue;
                }

                if (paidUntil is null || day >= paidUntil)
                {
                    purchases.Add(new PlannedPurchase(member.Email, member.MembershipTypeName, member.Method, day));
                    paidUntil = day.AddDays(duration);
                }

                // Weeks counted from the first history day: 0, 1, 2, 3.
                var week = (day.DayNumber - first.DayNumber) / 7;
                visits.AddRange(
                    todays.Select(v => new PlannedVisit(
                        member.Email,
                        v.WorkspaceCode,
                        day,
                        v.From,
                        v.To,
                        v.CancelledEveryOtherWeek && week % 2 == 1
                    ))
                );
            }
        }

        return new DemoHistoryPlan(
            [.. purchases.OrderBy(p => p.Day)],
            [.. visits.OrderBy(v => v.Day).ThenBy(v => v.From)]
        );
    }
}
