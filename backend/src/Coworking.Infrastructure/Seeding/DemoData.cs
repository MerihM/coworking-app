using Coworking.Domain.Enums;

namespace Coworking.Infrastructure.Seeding;

/// <summary>
/// The demo coworking center, modelled on the design document (chapter 1.1): an open space with
/// 30 desks (flexible and fixed), three private offices and two meeting rooms; daily, weekly and
/// monthly memberships. One place for all values, so the README and tests can refer to them.
/// </summary>
public static class DemoData
{
    /// <summary>A room with its workspaces.</summary>
    /// <param name="Name">Room name.</param>
    /// <param name="Floor">Floor.</param>
    /// <param name="Description">Short description shown in search results.</param>
    /// <param name="Workspaces">Code, type and capacity of each workspace.</param>
    public sealed record RoomSeed(
        string Name,
        int Floor,
        string Description,
        IReadOnlyList<(string Code, WorkspaceType Type, int Capacity)> Workspaces
    );

    /// <summary>A membership type of the price list.</summary>
    /// <param name="Name">Name.</param>
    /// <param name="DurationDays">Duration in days.</param>
    /// <param name="Price">Price in KM.</param>
    /// <param name="IsActive">false = shown only in the manager's list (to demo the inactive filter).</param>
    public sealed record MembershipTypeSeed(string Name, int DurationDays, decimal Price, bool IsActive);

    /// <summary>A demo login. <paramref name="Role"/> null = a member, otherwise an employee role.</summary>
    /// <param name="Email">Login e-mail.</param>
    /// <param name="FirstName">First name.</param>
    /// <param name="LastName">Last name.</param>
    /// <param name="Role">Employee role, or null for a member.</param>
    public sealed record AccountSeed(string Email, string FirstName, string LastName, EmployeeRole? Role);

    /// <summary>6 rooms, 35 workspaces: 24 hot desks + 6 fixed desks, 3 offices, 2 meeting rooms.</summary>
    public static IReadOnlyList<RoomSeed> Rooms { get; } =
    [
        new(
            "Open Space",
            1,
            "Bright open-plan area with 30 desks; quiet zone by the windows.",
            [
                .. Enumerable.Range(1, 24).Select(i => ($"OS-{i:00}", WorkspaceType.HotDesk, 1)),
                .. Enumerable.Range(25, 6).Select(i => ($"OS-{i:00}", WorkspaceType.FixedDesk, 1)),
            ]
        ),
        new("Office 1", 2, "Private office for a small team.", [("OF-1", WorkspaceType.Office, 4)]),
        new("Office 2", 2, "Private office for a small team.", [("OF-2", WorkspaceType.Office, 4)]),
        new("Office 3", 2, "Larger private office with a whiteboard wall.", [("OF-3", WorkspaceType.Office, 6)]),
        new("Meeting Room A", 2, "Meeting room with a TV screen.", [("MR-A", WorkspaceType.MeetingRoom, 8)]),
        new("Meeting Room B", 2, "Large meeting room with a projector.", [("MR-B", WorkspaceType.MeetingRoom, 12)]),
    ];

    /// <summary>The price list (KM). The inactive one demonstrates "no longer offered".</summary>
    public static IReadOnlyList<MembershipTypeSeed> MembershipTypes { get; } =
    [
        new("Daily", 1, 15.00m, IsActive: true),
        new("Weekly", 7, 60.00m, IsActive: true),
        new("Monthly", 30, 180.00m, IsActive: true),
        new("Student Monthly", 30, 120.00m, IsActive: false),
    ];

    /// <summary>
    /// One account per role that the administrator would otherwise have to create first. The
    /// administrator itself comes from <c>Seed:Admin</c> (AdminSeeder).
    /// </summary>
    public static IReadOnlyList<AccountSeed> Accounts { get; } =
    [
        new("receptionist@coworking.ba", "Emir", "Kovač", EmployeeRole.Receptionist),
        new("manager@coworking.ba", "Lejla", "Begić", EmployeeRole.Manager),
        new("member@coworking.ba", "Amra", "Hodžić", null),
    ];

    /// <summary>
    /// Membership type the demo member gets, already paid (CW-26), so the demo member can make
    /// reservations right away (Sprint 5, BR02).
    /// </summary>
    public const string DemoMembershipTypeName = "Monthly";

    /// <summary>An upcoming demo reservation of the demo member (CW-29).</summary>
    /// <param name="WorkspaceCode">Workspace from <see cref="Rooms"/>.</param>
    /// <param name="From">Local start time.</param>
    /// <param name="To">Local end time.</param>
    public sealed record ReservationSeed(string WorkspaceCode, TimeOnly From, TimeOnly To);

    /// <summary>
    /// Two reservations on the next weekday, so "My reservations", the reception list and the report
    /// have data. Afternoon times on purpose: the seeder test searches 10:00–12:00 and expects every
    /// workspace to be free then.
    /// </summary>
    public static IReadOnlyList<ReservationSeed> Reservations { get; } =
    [
        new("OS-01", new TimeOnly(13, 0), new TimeOnly(17, 0)),
        new("MR-A", new TimeOnly(14, 0), new TimeOnly(16, 0)),
    ];

    /// <summary>Phone number of the demo member (required field).</summary>
    public const string MemberPhone = "+387 61 123 456";

    // ------------------------------------------------------------------ history (CW-32, FR11 report)

    /// <summary>How many days of past activity the history members get (4 weeks before today).</summary>
    public const int HistoryDays = 28;

    /// <summary>A weekly habit of a history member: every <paramref name="Day"/> they use one workspace.</summary>
    /// <param name="Day">Day of the week.</param>
    /// <param name="WorkspaceCode">Workspace from <see cref="Rooms"/>.</param>
    /// <param name="From">Local start time.</param>
    /// <param name="To">Local end time.</param>
    /// <param name="CancelledEveryOtherWeek">
    /// In every second week the member cancels this visit in the morning (shows "cancelled" in the
    /// report). Such visits start at 10:00 or later, so the cancellation keeps BR03 (2 h before).
    /// </param>
    public sealed record HistoryVisitSeed(
        DayOfWeek Day,
        string WorkspaceCode,
        TimeOnly From,
        TimeOnly To,
        bool CancelledEveryOtherWeek = false
    );

    /// <summary>
    /// A member with 4 weeks of past activity: they buy <paramref name="MembershipTypeName"/> whenever
    /// they need one (BR02), pay it at the desk (BR05) and come by according to <paramref name="Visits"/>.
    /// </summary>
    /// <param name="Email">Login e-mail (demo password, like the other demo accounts).</param>
    /// <param name="FirstName">First name.</param>
    /// <param name="LastName">Last name.</param>
    /// <param name="Phone">Phone.</param>
    /// <param name="MembershipTypeName">Membership type from <see cref="MembershipTypes"/>.</param>
    /// <param name="Method">How they pay.</param>
    /// <param name="Visits">Their weekly habits.</param>
    public sealed record HistoryMemberSeed(
        string Email,
        string FirstName,
        string LastName,
        string Phone,
        string MembershipTypeName,
        PaymentMethod Method,
        IReadOnlyList<HistoryVisitSeed> Visits
    );

    /// <summary>
    /// Five regulars, so the FR11 report has realistic past occupancy and revenue (the domain itself
    /// refuses reservations in the past, so real use can't create history in a fresh database).
    /// Each member uses their own workspaces at their own times – the plan never breaks BR01.
    /// </summary>
    public static IReadOnlyList<HistoryMemberSeed> HistoryMembers { get; } =
    [
        new(
            "tarik.mehic@coworking.ba",
            "Tarik",
            "Mehić",
            "+387 61 200 001",
            "Monthly",
            PaymentMethod.Card,
            [
                new(DayOfWeek.Monday, "OS-02", new(9, 0), new(17, 0)),
                new(DayOfWeek.Tuesday, "OS-02", new(9, 0), new(17, 0)),
                new(DayOfWeek.Wednesday, "OS-02", new(9, 0), new(17, 0)),
                new(DayOfWeek.Wednesday, "MR-A", new(17, 0), new(19, 0)),
                new(DayOfWeek.Thursday, "OS-02", new(9, 0), new(17, 0)),
                new(DayOfWeek.Friday, "OS-02", new(9, 0), new(13, 0)),
            ]
        ),
        new(
            "sara.kovacevic@coworking.ba",
            "Sara",
            "Kovačević",
            "+387 61 200 002",
            "Weekly",
            PaymentMethod.Cash,
            [
                new(DayOfWeek.Monday, "OS-05", new(10, 0), new(16, 0), CancelledEveryOtherWeek: true),
                new(DayOfWeek.Tuesday, "OS-05", new(8, 0), new(16, 0)),
                new(DayOfWeek.Wednesday, "OF-1", new(9, 0), new(17, 0)),
                new(DayOfWeek.Thursday, "OS-05", new(8, 0), new(16, 0)),
                new(DayOfWeek.Saturday, "OS-05", new(10, 0), new(14, 0)),
            ]
        ),
        new(
            "haris.delic@coworking.ba",
            "Haris",
            "Delić",
            "+387 61 200 003",
            "Daily",
            PaymentMethod.Cash,
            [
                new(DayOfWeek.Tuesday, "OF-2", new(9, 0), new(17, 0)),
                new(DayOfWeek.Thursday, "MR-B", new(10, 0), new(12, 0)),
                new(DayOfWeek.Thursday, "OS-10", new(12, 0), new(18, 0)),
                new(DayOfWeek.Saturday, "OS-10", new(9, 0), new(13, 0)),
            ]
        ),
        new(
            "dino.hasanovic@coworking.ba",
            "Dino",
            "Hasanović",
            "+387 61 200 004",
            "Monthly",
            PaymentMethod.Card,
            [
                new(DayOfWeek.Monday, "OS-03", new(8, 0), new(16, 0)),
                new(DayOfWeek.Monday, "MR-B", new(15, 0), new(17, 0)),
                new(DayOfWeek.Tuesday, "OF-3", new(9, 0), new(18, 0)),
                new(DayOfWeek.Wednesday, "OS-03", new(8, 0), new(16, 0)),
                new(DayOfWeek.Thursday, "OF-3", new(9, 0), new(18, 0)),
                new(DayOfWeek.Friday, "OS-03", new(8, 0), new(16, 0)),
            ]
        ),
        new(
            "ajla.mujic@coworking.ba",
            "Ajla",
            "Mujić",
            "+387 61 200 005",
            "Weekly",
            PaymentMethod.Card,
            [
                new(DayOfWeek.Monday, "OS-04", new(12, 0), new(20, 0)),
                new(DayOfWeek.Tuesday, "OS-04", new(12, 0), new(20, 0), CancelledEveryOtherWeek: true),
                new(DayOfWeek.Wednesday, "OS-04", new(12, 0), new(20, 0)),
                new(DayOfWeek.Thursday, "OS-04", new(12, 0), new(20, 0)),
                new(DayOfWeek.Friday, "OS-04", new(12, 0), new(20, 0)),
                new(DayOfWeek.Friday, "MR-A", new(15, 0), new(17, 0)),
            ]
        ),
    ];
}
