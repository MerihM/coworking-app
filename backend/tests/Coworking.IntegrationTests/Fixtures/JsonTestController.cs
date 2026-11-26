using Coworking.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// Test-only endpoint that echoes a body with a number and an enum, to check the JSON rules
/// from Program.cs (strict numbers, enums only as names). Added by AuthTestingApiFactory.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("test/json")]
public sealed class JsonTestController : ControllerBase
{
    [HttpPost("echo")]
    public ActionResult<JsonEchoBody> Echo(JsonEchoBody body) => body;
}

public sealed record JsonEchoBody(int Count, EmployeeRole Role);
