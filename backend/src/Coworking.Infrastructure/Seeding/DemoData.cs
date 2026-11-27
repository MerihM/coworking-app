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

    /// <summary>Phone number of the demo member (required field).</summary>
    public const string MemberPhone = "+387 61 123 456";
}
