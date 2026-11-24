using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using static Coworking.UnitTests.Domain.TestData;

namespace Coworking.UnitTests.Domain;

/// <summary>Member rules: e-mail normalization, deactivation, fixed desk.</summary>
public class MemberTests
{
    [Fact]
    public void Create_NormalizesEmailToLowerCase()
    {
        Assert.Equal("amra@example.com", NewMember().Email);
    }

    [Fact]
    public void PurchaseMembership_DeactivatedMember_Throws()
    {
        var member = NewMember();
        member.Deactivate();

        Assert.Throws<DomainException>(() => member.PurchaseMembership(NewMonthlyType(), Now));
    }

    [Fact]
    public void PurchaseMembership_TypeNoLongerOffered_Throws()
    {
        var type = NewMonthlyType();
        type.Deactivate();

        Assert.Throws<DomainException>(() => NewMember().PurchaseMembership(type, Now));
    }

    [Fact]
    public void AssignFixedWorkspace_HotDesk_Throws()
    {
        Assert.Throws<DomainException>(() =>
            NewMember().AssignFixedWorkspace(NewDesk("A1", WorkspaceType.HotDesk))
        );
    }

    [Fact]
    public void AssignFixedWorkspace_FixedDesk_IsAssigned()
    {
        var member = NewMember();
        var desk = NewDesk("F1", WorkspaceType.FixedDesk);

        member.AssignFixedWorkspace(desk);

        Assert.Same(desk, member.FixedWorkspace);
    }
}
