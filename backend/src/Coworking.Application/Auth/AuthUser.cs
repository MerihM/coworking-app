using Coworking.Domain.Entities;

namespace Coworking.Application.Auth;

/// <summary>
/// The minimum we need to know about a user to issue a token for them. Built from a
/// <see cref="Member"/> or an <see cref="Employee"/> with <see cref="FromMember"/> /
/// <see cref="FromEmployee"/>.
/// </summary>
/// <param name="Id">ID inside the user's own table.</param>
/// <param name="UserType">Which table the ID belongs to.</param>
/// <param name="Role">One of <see cref="Roles"/>.</param>
/// <param name="Email">Normalized (trimmed, lowercase) e-mail address.</param>
/// <param name="FullName">Display name, e.g. "Amra Hodzic".</param>
public sealed record AuthUser(int Id, UserType UserType, string Role, string Email, string FullName)
{
    /// <summary>Creates the token identity of a member. Every member has the role <see cref="Roles.Member"/>.</summary>
    /// <param name="member">A saved member (Id &gt; 0).</param>
    /// <returns>The identity to put into tokens.</returns>
    public static AuthUser FromMember(Member member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return new AuthUser(
            member.Id,
            UserType.Member,
            Roles.Member,
            member.Email,
            $"{member.FirstName} {member.LastName}"
        );
    }

    /// <summary>
    /// Creates the token identity of an employee. The role is the enum name, which matches the
    /// constants in <see cref="Roles"/> (guarded by RolesTests).
    /// </summary>
    /// <param name="employee">A saved employee (Id &gt; 0).</param>
    /// <returns>The identity to put into tokens.</returns>
    public static AuthUser FromEmployee(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);
        return new AuthUser(
            employee.Id,
            UserType.Employee,
            employee.Role.ToString(),
            employee.Email,
            $"{employee.FirstName} {employee.LastName}"
        );
    }
}
