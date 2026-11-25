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

    /// <summary>
    /// 401 – wrong e-mail or password, or a deactivated account. One code for all three on
    /// purpose: a different answer for "unknown e-mail" would tell an attacker which e-mails
    /// have accounts.
    /// </summary>
    public const string InvalidCredentials = "auth.invalid_credentials";

    /// <summary>409 – registration with an e-mail that a member or employee already uses.</summary>
    public const string EmailTaken = "auth.email_taken";

    /// <summary>401 – the refresh cookie is missing, expired, revoked or reused: log in again.</summary>
    public const string RefreshInvalid = "auth.refresh_invalid";

    /// <summary>429 – too many login/register attempts from this address; see Retry-After.</summary>
    public const string TooManyRequests = "auth.too_many_requests";
}
