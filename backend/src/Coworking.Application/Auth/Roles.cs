namespace Coworking.Application.Auth;

/// <summary>
/// Role names used in tokens and authorization policies (design document: actors in chapter 3).
/// </summary>
/// <remarks>
/// The three staff roles must match the names of <c>Coworking.Domain.Enums.EmployeeRole</c> exactly,
/// because an employee's role claim is produced with <c>EmployeeRole.ToString()</c>. A unit test
/// (<c>RolesTests</c>) guards this. <see cref="Member"/> is not an EmployeeRole – every member has it.
/// The Visitor (Posjetilac) actor has no role: it is simply an anonymous request.
/// </remarks>
public static class Roles
{
    /// <summary>Registered member (Clan).</summary>
    public const string Member = "Member";

    /// <summary>Receptionist (Recepcioner): records payments, manages members, cancels reservations.</summary>
    public const string Receptionist = "Receptionist";

    /// <summary>Manager (Menadzer): rooms, workspaces, membership types, reports.</summary>
    public const string Manager = "Manager";

    /// <summary>Administrator: staff accounts and roles (FR12).</summary>
    public const string Administrator = "Administrator";

    /// <summary>Every role the system knows, in a fixed order.</summary>
    public static IReadOnlyList<string> All { get; } = [Member, Receptionist, Manager, Administrator];
}
