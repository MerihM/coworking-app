using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using static Coworking.UnitTests.Domain.TestData;

namespace Coworking.UnitTests.Domain;

/// <summary>Memberships and payments: BR05, TC07.</summary>
public class MembershipTests
{
    [Fact]
    public void Purchase_CreatesUnpaidMembershipThatIsNotActive()
    {
        var membership = NewMember().PurchaseMembership(NewMonthlyType(), Now);

        Assert.Equal(MembershipStatus.Unpaid, membership.Status);
        Assert.False(membership.IsActiveOn(new DateOnly(2026, 11, 23))); // BR05: not before payment
    }

    [Fact]
    public void Purchase_KeepsPriceFromMomentOfPurchase()
    {
        var type = NewMonthlyType();
        var membership = NewMember().PurchaseMembership(type, Now);

        type.Update("Monthly", 30, 150m); // the manager raises the price later

        Assert.Equal(120m, membership.Price);
    }

    [Fact]
    public void TC07_RecordPayment_FullAmount_ActivatesFromPaymentDate()
    {
        var membership = NewMember().PurchaseMembership(NewMonthlyType(), Now);

        var payment = NewReceptionist().RecordPayment(membership, 120m, PaymentMethod.Card, Now);

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(new DateOnly(2026, 11, 23), membership.StartDate);
        Assert.Equal(new DateOnly(2026, 12, 23), membership.EndDate); // start + 30 days
        Assert.Same(payment, membership.Payment);
    }

    [Fact]
    public void IsActiveOn_EndDateIsExclusive()
    {
        var membership = NewMember().PurchaseMembership(NewMonthlyType(), Now);
        NewReceptionist().RecordPayment(membership, 120m, PaymentMethod.Cash, Now);

        Assert.False(membership.IsActiveOn(new DateOnly(2026, 11, 22))); // day before start
        Assert.True(membership.IsActiveOn(new DateOnly(2026, 11, 23))); // first day
        Assert.True(membership.IsActiveOn(new DateOnly(2026, 12, 22))); // last valid day
        Assert.False(membership.IsActiveOn(new DateOnly(2026, 12, 23))); // EndDate = no longer valid
    }

    [Fact]
    public void BR05_RecordPayment_Renewal_StartsOnExistingEndDate()
    {
        var member = MemberWithPaidMembership(Now); // valid 23 Nov → 23 Dec (exclusive)
        var renewalDate = At(2026, 12, 10, 10);
        var renewal = member.PurchaseMembership(NewMonthlyType(), renewalDate);

        NewReceptionist().RecordPayment(renewal, 120m, PaymentMethod.Cash, renewalDate);

        Assert.Equal(new DateOnly(2026, 12, 23), renewal.StartDate); // no gap, no overlap
        Assert.Equal(new DateOnly(2027, 1, 22), renewal.EndDate);
    }

    [Fact]
    public void RecordPayment_WrongAmount_ThrowsBr05()
    {
        var membership = NewMember().PurchaseMembership(NewMonthlyType(), Now);

        var exception = Assert.Throws<DomainException>(() =>
            NewReceptionist().RecordPayment(membership, 100m, PaymentMethod.Cash, Now)
        );

        Assert.Equal("BR05", exception.RuleCode);
        Assert.Equal(MembershipStatus.Unpaid, membership.Status);
    }

    [Fact]
    public void RecordPayment_AlreadyPaid_Throws()
    {
        var membership = NewMember().PurchaseMembership(NewMonthlyType(), Now);
        var receptionist = NewReceptionist();
        receptionist.RecordPayment(membership, 120m, PaymentMethod.Cash, Now);

        Assert.Throws<DomainException>(() =>
            receptionist.RecordPayment(membership, 120m, PaymentMethod.Cash, Now)
        );
    }
}
