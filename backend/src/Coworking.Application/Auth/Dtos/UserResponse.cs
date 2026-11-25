namespace Coworking.Application.Auth.Dtos;

/// <summary>
/// The logged-in user as the frontend sees it (login/register/refresh responses and <c>GET /api/auth/me</c>).
/// </summary>
/// <param name="Id">Id inside the user's own table.</param>
/// <param name="UserType"><c>"member"</c> or <c>"employee"</c> (same values as the token claim).</param>
/// <param name="Role">Member, Receptionist, Manager or Administrator – drives navigation and route guards.</param>
/// <param name="Email">E-mail (lower case).</param>
/// <param name="FullName">Display name for the header/user menu.</param>
public sealed record UserResponse(int Id, string UserType, string Role, string Email, string FullName)
{
    /// <summary>Builds the response from the token identity.</summary>
    /// <param name="user">The authenticated user.</param>
    /// <returns>The response object.</returns>
    public static UserResponse From(AuthUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserResponse(
            user.Id,
            UserTypeClaimValues.From(user.UserType),
            user.Role,
            user.Email,
            user.FullName
        );
    }
}
