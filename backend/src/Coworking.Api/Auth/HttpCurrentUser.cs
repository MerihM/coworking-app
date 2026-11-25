using System.Globalization;
using System.Security.Claims;
using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;

namespace Coworking.Api.Auth;

/// <summary>
/// <see cref="ICurrentUser"/> that reads the claims of the current HTTP request.
/// </summary>
/// <remarks>
/// Lives in the Api project because it needs <see cref="HttpContext"/>; Application only sees the
/// interface. Registered as scoped (one instance per request).
/// </remarks>
/// <param name="httpContextAccessor">Gives access to the current request's <see cref="HttpContext"/>.</param>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    /// <summary>The validated user from the JwtBearer handler, or null outside a request.</summary>
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && Id is not null;

    /// <inheritdoc />
    public int? Id =>
        int.TryParse(
            Principal?.FindFirstValue(AuthClaimTypes.Subject),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var id)
            ? id
            : null;

    /// <inheritdoc />
    public UserType? UserType =>
        UserTypeClaimValues.TryParse(Principal?.FindFirstValue(AuthClaimTypes.UserType), out var userType)
            ? userType
            : null;

    /// <inheritdoc />
    public string? Role => Principal?.FindFirstValue(AuthClaimTypes.Role);
}
