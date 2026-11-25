using Coworking.Application.Auth;

namespace Coworking.Api.Auth;

/// <summary>
/// Reads, writes and deletes the refresh token cookie. All cookie settings live here, so
/// writing and deleting always use the same attributes (a cookie is only deleted when name AND
/// path match the original).
/// </summary>
/// <remarks>
/// Attributes and why (ADR 0003):
/// <list type="bullet">
/// <item><b>HttpOnly</b> – JavaScript can't read it, so an XSS bug can't steal it.</item>
/// <item><b>Secure</b> – only sent over HTTPS. Browsers treat <c>http://localhost</c> as secure, so
/// local development still works.</item>
/// <item><b>SameSite=Strict</b> – never sent on requests started by another site (CSRF protection).</item>
/// <item><b>Path=/api/auth</b> – only sent to refresh/logout, not with every API call.</item>
/// <item><b>Expires</b> – same as the token, so the browser drops it when it is useless anyway.</item>
/// </list>
/// </remarks>
public static class RefreshTokenCookie
{
    /// <summary>Cookie name.</summary>
    public const string Name = "cw_refresh";

    /// <summary>The only path the browser sends the cookie to.</summary>
    public const string CookiePath = "/api/auth";

    /// <summary>Puts a newly issued refresh token into the response.</summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="token">The token from IssueAsync/RotateAsync.</param>
    public static void Append(HttpResponse response, IssuedRefreshToken token)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(token);
        response.Cookies.Append(Name, token.Token, CreateOptions(token.ExpiresAt));
    }

    /// <summary>
    /// Tells the browser to delete the cookie (it sends it back with an expiry date in 1970).
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    public static void Delete(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        response.Cookies.Delete(Name, CreateOptions(expires: null));
    }

    /// <summary>Reads the raw refresh token from the request, if the browser sent one.</summary>
    /// <param name="request">The HTTP request.</param>
    /// <returns>The cookie value, or null.</returns>
    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Cookies[Name];
    }

    private static CookieOptions CreateOptions(DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            Expires = expires,
            // "Essential" = needed for the site to work, so a cookie-consent policy never blocks it.
            IsEssential = true,
        };
}
