using Coworking.Domain.Enums;
using static Coworking.UnitTests.Domain.TestData;

namespace Coworking.UnitTests.Domain;

/// <summary>
/// CW-26: the status a user sees (derived "Expired") and TC02's domain part – a member whose
/// membership has expired or isn't paid has no active membership, so BR02 will refuse the
/// reservation (the API part of TC02 follows with the reservations in Sprint 5).
/// </summary>
public class MembershipStatusTests
{
    // Paid on Monday 2026-11-23 (TestData.Now): valid 2026-11-23 … 2026-12-22, EndDate 2026-12-23.
    private static readonly DateOnly PaymentDay = new(2026, 11, 23);
    private static readonly DateOnly EndDate = PaymentDay.AddDays(30);

    [Fact]
    public void TC02_ExpiredMembership_IsNotActive()
    {
        var member = MemberWithPaidMembership(Now);

        Assert.True(member.HasActiveMembershipOn(EndDate.AddDays(-1)));
        Assert.False(member.HasActiveMembershipOn(EndDate));
        Assert.False(member.HasActiveMembershipOn(EndDate.AddDays(10)));
    }

    [Fact]
    public void TC02_UnpaidMembership_IsNotActive()
    {
        var member = NewMember();
        var membership = member.PurchaseMembership(NewMonthlyType(), Now);

        Assert.False(member.HasActiveMembershipOn(PaymentDay));
        Assert.Equal(MembershipStatus.Unpaid, membership.EffectiveStatusOn(PaymentDay));
    }

    [Fact]
    public void EffectiveStatus_IsActiveUntilTheLastDay_ThenExpired()
    {
        var membership = MemberWithPaidMembership(Now).Memberships.Single();

        Assert.Equal(MembershipStatus.Active, membership.EffectiveStatusOn(PaymentDay));
        Assert.Equal(MembershipStatus.Active, membership.EffectiveStatusOn(EndDate.AddDays(-1)));
        Assert.Equal(MembershipStatus.Expired, membership.EffectiveStatusOn(EndDate));
        // Derived only: the stored status doesn't change by looking at it.
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public void Member_ChangeEmail_TrimsAndLowerCases()
    {
        var member = NewMember();

        member.ChangeEmail("  New.Address@Example.COM ");

        Assert.Equal("new.address@example.com", member.Email);
    }
}
