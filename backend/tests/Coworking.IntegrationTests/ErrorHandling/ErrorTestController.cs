using Coworking.Application.Common.Exceptions;
using Coworking.Domain.Common;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.IntegrationTests.ErrorHandling;

/// <summary>
/// Test-only controller: each action throws one exception type, so the tests can check
/// how GlobalExceptionHandler maps it. Lives in the TEST assembly, so it never exists in
/// the real API; ErrorTestingApiFactory plugs it in only for these tests.
/// </summary>
[ApiController]
[Route("test/errors")]
public sealed class ErrorTestController : ControllerBase
{
    [HttpGet("not-found")]
    public IActionResult ThrowNotFound() => throw new NotFoundException("Workspace", 42);

    [HttpGet("conflict")]
    public IActionResult ThrowConflict() =>
        throw new ConflictException(
            "Workspace A1 is already reserved in the requested period.",
            "BR01"
        );

    [HttpGet("business-rule")]
    public IActionResult ThrowBusinessRule() =>
        throw new BusinessRuleException(
            "BR04",
            "On Saturday reservations are allowed only between 09:00 and 17:00."
        );

    [HttpGet("forbidden")]
    public IActionResult ThrowForbidden() =>
        throw new ForbiddenException("You can only cancel your own reservations.");

    [HttpGet("validation")]
    public IActionResult ThrowValidation() =>
        throw new ValidationException(
            new List<ValidationFailure>
            {
                new("Email", "Email is required."),
                new("Email", "Email is not a valid e-mail address."),
                new("Phone", "Phone is required."),
            }
        );

    [HttpGet("unexpected")]
    public IActionResult ThrowUnexpected() =>
        throw new InvalidOperationException("Something broke deep inside.");

    [HttpGet("domain-rule")]
    public IActionResult ThrowDomainRule() =>
        throw new DomainException(
            "Members can cancel a reservation at the latest 2 hours before it starts.",
            "BR03"
        );
}
