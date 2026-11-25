using Coworking.Api.Auth;
using Coworking.Api.RateLimiting;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coworking.Api.Controllers;

/// <summary>
/// Registration, login and session handling (FR01, UC01, NFR01).
/// </summary>
/// <remarks>
/// The controller only deals with HTTP: it reads the body and the cookie, calls
/// <see cref="IAuthService"/>, and writes the body and the cookie. All rules live in the service.
/// Errors (400 validation, 401, 409) are thrown by the service and turned into ProblemDetails
/// by GlobalExceptionHandler.
/// No [Produces] attribute on purpose: it would overwrite the "application/problem+json" content
/// type of <see cref="AuthProblem"/>. Each response type names its content type instead.
/// </remarks>
/// <param name="auth">The auth use cases.</param>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    /// <summary>Registers a new member and logs them in (FR01).</summary>
    /// <remarks>Returns the access token in the body and sets the refresh token cookie.</remarks>
    /// <param name="request">Name, surname, e-mail, phone and password.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Registered and logged in.</response>
    /// <response code="400">Invalid input; see "errors".</response>
    /// <response code="409">E-mail already used (code auth.email_taken).</response>
    /// <response code="429">Too many attempts (code auth.too_many_requests).</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterMemberRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await auth.RegisterMemberAsync(request, cancellationToken);
        RefreshTokenCookie.Append(Response, result.RefreshToken);

        // 201 Created with "Location: /api/auth/me" – where the new account can be read.
        return CreatedAtAction(nameof(Me), result.Response);
    }

    /// <summary>Logs in a member or an employee.</summary>
    /// <remarks>Returns the access token in the body and sets the refresh token cookie.</remarks>
    /// <param name="request">E-mail and password.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">Logged in.</response>
    /// <response code="400">E-mail or password missing.</response>
    /// <response code="401">Wrong e-mail/password or deactivated account (code auth.invalid_credentials).</response>
    /// <response code="429">Too many attempts (code auth.too_many_requests).</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingSetup.AuthPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken
    )
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        RefreshTokenCookie.Append(Response, result.RefreshToken);
        return Ok(result.Response);
    }

    /// <summary>Exchanges the refresh cookie for a new access token (and a new refresh cookie).</summary>
    /// <remarks>
    /// No body: the browser sends the httpOnly cookie automatically. Called by the frontend on
    /// page load (silent login) and when an access token expires.
    /// </remarks>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">New session.</response>
    /// <response code="401">Cookie missing, expired, revoked or reused (code auth.refresh_invalid); the cookie is deleted.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await auth.RefreshAsync(RefreshTokenCookie.Read(Request), cancellationToken);

        if (result is null)
        {
            // Returned here instead of thrown: the exception handler clears all response
            // headers before writing the error, which would also remove our "delete cookie" header.
            RefreshTokenCookie.Delete(Response);
            return AuthProblem(
                StatusCodes.Status401Unauthorized,
                AuthErrorCodes.RefreshInvalid,
                "Your session has expired. Please log in again."
            );
        }

        RefreshTokenCookie.Append(Response, result.RefreshToken);
        return Ok(result.Response);
    }

    /// <summary>Logs out: revokes the session and deletes the refresh cookie.</summary>
    /// <remarks>Anonymous on purpose, so logout also works when the access token has already expired.</remarks>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Logged out (also when there was no session).</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(RefreshTokenCookie.Read(Request), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    /// <summary>Returns the logged-in user, loaded fresh from the database.</summary>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The current user.</response>
    /// <response code="401">No valid access token, or the account was deactivated.</response>
    [HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken) =>
        await auth.GetCurrentUserAsync(cancellationToken);

    /// <summary>
    /// Builds a ProblemDetails response with our "code" field. ProblemDetailsFactory applies the
    /// same defaults as every other error (type, title, traceId from Program.cs).
    /// </summary>
    private ObjectResult AuthProblem(int statusCode, string code, string detail)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext,
            statusCode: statusCode,
            detail: detail
        );
        problem.Extensions["code"] = code;

        return new ObjectResult(problem)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" },
        };
    }
}
