namespace Coworking.Application.Auth;

/// <summary>
/// Which table an authenticated user comes from.
/// </summary>
/// <remarks>
/// The class diagram keeps members (Clan) and employees (Zaposlenik) as two separate classes,
/// so their IDs are only unique inside their own table. Member 5 and employee 5 are different
/// people – every place that identifies a user therefore needs the pair (UserType, Id).
/// </remarks>
public enum UserType
{
    /// <summary>A registered member of the coworking center (role <see cref="Roles.Member"/>).</summary>
    Member,

    /// <summary>A staff account: receptionist, manager or administrator.</summary>
    Employee,
}
