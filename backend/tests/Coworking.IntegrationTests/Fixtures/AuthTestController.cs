using Coworking.Application.Auth;
using Coworking.Application.Common.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// Test-only endpoints for checking authentication and authorization before real protected
/// endpoints exist. Added to the app only by <see cref="AuthTestingApiFactory"/>.
/// No [Authorize] on the class on purpose: "whoami" is protected by the
/// <c>MapControllers().RequireAuthorization()</c> convention in Program.cs, which is what we test.
/// </summary>
[ApiController]
[Route("test/auth")]
public sealed class AuthTestController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("whoami")]
    public ActionResult<WhoAmIResponse> WhoAmI() =>
        new WhoAmIResponse(currentUser.IsAuthenticated, currentUser.Id, currentUser.UserType?.ToString(), currentUser.Role);

    [HttpGet("manager")]
    [Authorize(Policy = Policies.ManagerOnly)]
    public IActionResult ManagerOnly() => Ok();

    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult Anonymous() => Ok();
}

public sealed record WhoAmIResponse(bool IsAuthenticated, int? Id, string? UserType, string? Role);
