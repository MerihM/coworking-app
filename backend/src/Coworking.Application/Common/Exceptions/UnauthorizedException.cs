namespace Coworking.Application.Common.Exceptions;

/// <summary>
/// The caller could not be authenticated: wrong e-mail or password, or the account behind a
/// valid token no longer exists or was deactivated. Mapped to HTTP 401.
/// </summary>
/// <remarks>
/// Not the same as <see cref="ForbiddenException"/> (403): 401 means "we don't know who you are,
/// log in (again)", 403 means "we know who you are, and you may not do this".
/// </remarks>
public sealed class UnauthorizedException : AppException
{
    /// <summary>Creates an authentication error.</summary>
    /// <param name="message">Explanation for the user (English fallback).</param>
    /// <param name="code">Error code for the frontend, e.g. "auth.invalid_credentials".</param>
    public UnauthorizedException(string message, string code)
        : base(message, ruleCode: null, code: code) { }
}
