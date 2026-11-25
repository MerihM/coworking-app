namespace Coworking.Application.Auth;

/// <summary>
/// The minimum we need to know about a user to issue a token for them. It is built from a
/// <c>Member</c> or an <c>Employee</c> entity after a successful login (CW-11).
/// </summary>
/// <param name="Id">ID inside the user's own table.</param>
/// <param name="UserType">Which table the ID belongs to.</param>
/// <param name="Role">One of <see cref="Roles"/>.</param>
/// <param name="Email">Normalized (trimmed, lowercase) e-mail address.</param>
/// <param name="FullName">Display name, e.g. "Amra Hodzic".</param>
public sealed record AuthUser(int Id, UserType UserType, string Role, string Email, string FullName);
