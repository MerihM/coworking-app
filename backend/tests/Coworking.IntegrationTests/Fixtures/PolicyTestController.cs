using Coworking.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// One test-only endpoint per authorization policy, so every policy can be tested before the
/// real endpoints that use it exist (rooms, reports, reservations came in Sprints 3–6).
/// The FR11 stand-in that TC08 used until Sprint 6 is gone: TC08 now runs against the real
/// report endpoint in Reports/ReportsEndpointsTests (CW-32).
/// Added to the app by AuthTestingApiFactory (it registers the whole test assembly).
/// </summary>
[ApiController]
[Route("test/policies")]
public sealed class PolicyTestController : ControllerBase
{
    [HttpGet("authenticated")] // no attribute: protected by MapControllers().RequireAuthorization()
    public IActionResult Authenticated() => Ok();

    [HttpGet("administrator")]
    [Authorize(Policy = Policies.AdministratorOnly)]
    public IActionResult Administrator() => Ok();

    [HttpGet("manager")]
    [Authorize(Policy = Policies.ManagerOnly)]
    public IActionResult Manager() => Ok();

    [HttpGet("receptionist-or-manager")]
    [Authorize(Policy = Policies.ReceptionistOrManager)]
    public IActionResult ReceptionistOrManager() => Ok();

    [HttpGet("staff")]
    [Authorize(Policy = Policies.StaffOnly)]
    public IActionResult Staff() => Ok();

    [HttpGet("member")]
    [Authorize(Policy = Policies.MemberOnly)]
    public IActionResult Member() => Ok();
}
