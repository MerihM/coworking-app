namespace Coworking.Application.Auth;

/// <summary>
/// Names of the claims we put into access tokens.
/// </summary>
/// <remarks>
/// We use the short, standard JWT names (<c>sub</c>, <c>role</c>, ...) and tell the JwtBearer handler
/// NOT to rename them on the way in (<c>MapInboundClaims = false</c>). Without that setting ASP.NET
/// would turn <c>sub</c> into the long legacy URI
/// <c>http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier</c>.
/// </remarks>
public static class AuthClaimTypes
{
    /// <summary>Subject – the user's ID inside its own table (see <see cref="UserType"/>).</summary>
    public const string Subject = "sub";

    /// <summary>Which table the user comes from: <c>member</c> or <c>employee</c>.</summary>
    public const string UserType = "user_type";

    /// <summary>One of the values in <see cref="Roles"/>; used by role-based policies.</summary>
    public const string Role = "role";

    /// <summary>The user's e-mail address.</summary>
    public const string Email = "email";

    /// <summary>The user's display name ("First Last").</summary>
    public const string Name = "name";

    /// <summary>Unique token ID; makes every token different and helps when reading logs.</summary>
    public const string TokenId = "jti";
}
