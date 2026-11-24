using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;

namespace Coworking.UnitTests.Domain;

/// <summary>Builders for valid test objects, so each test only shows what matters to it.</summary>
internal static class TestData
{
    /// <summary>"Now" in all tests: Monday 2026-11-23, 08:00 in Sarajevo (winter, UTC+1).</summary>
    public static readonly DateTimeOffset Now = At(2026, 11, 23, 8);

    /// <summary>A Sarajevo wall-clock time as a UTC instant.</summary>
    public static DateTimeOffset At(int year, int month, int day, int hour, int minute = 0) =>
        CenterTimeZone.FromLocal(new DateOnly(year, month, day), new TimeOnly(hour, minute));

    public static Member NewMember() =>
        new("Amra", "Hodžić", "Amra@Example.com", "+387 61 111 222", "hash", Now);

    public static Employee NewReceptionist() =>
        new("Emir", "Kovač", "emir@coworking.ba", "hash", EmployeeRole.Receptionist);

    public static MembershipType NewMonthlyType() => new("Monthly", 30, 120m);

    public static Workspace NewDesk(
        string code = "A1",
        WorkspaceType type = WorkspaceType.HotDesk
    ) => new Room("Open space", 1, null).AddWorkspace(code, type, 1);

    /// <summary>A member with a paid monthly membership (30 days from the payment date).</summary>
    public static Member MemberWithPaidMembership(DateTimeOffset paidAt)
    {
        var member = NewMember();
        var membership = member.PurchaseMembership(NewMonthlyType(), paidAt);
        NewReceptionist().RecordPayment(membership, membership.Price, PaymentMethod.Cash, paidAt);
        return member;
    }
}
