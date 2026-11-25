namespace Coworking.Application.Auth;

/// <summary>
/// Stable error codes for authentication problems. They go into the <c>code</c> field of
/// ProblemDetails responses; the frontend translates them (English/Bosnian) instead of showing
/// the English <c>detail</c> text. Never change an existing value – the frontend depends on it.
/// </summary>
public static class AuthErrorCodes
{
    /// <summary>401 – no access token, or the token is invalid/expired.</summary>
    public const string Unauthenticated = "auth.unauthenticated";

    /// <summary>403 – logged in, but the role is not allowed to use this function.</summary>
    public const string Forbidden = "auth.forbidden";
}
