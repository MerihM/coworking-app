using Coworking.Application.Auth;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Employees;
using Coworking.Application.Employees.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coworking.Api.Controllers;

/// <summary>
/// Staff account management (FR12, UC14). Administrator only – every action inherits the policy
/// from the class. Members, receptionists and managers get 403, anonymous callers 401.
/// </summary>
/// <param name="employees">The staff account use cases.</param>
[ApiController]
[Route("api/employees")]
[Authorize(Policy = Policies.AdministratorOnly)]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    /// <summary>Lists staff accounts, one page at a time, with optional search and filters.</summary>
    /// <param name="query">page, pageSize, search, role, isActive – all optional.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The requested page.</response>
    /// <response code="400">Invalid paging or filter values.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<EmployeeResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<EmployeeResponse>>> List(
        [FromQuery] ListEmployeesQuery query,
        CancellationToken cancellationToken
    ) => await employees.ListAsync(query, cancellationToken);

    /// <summary>Returns one staff account.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The account.</response>
    /// <response code="404">No employee with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<EmployeeResponse>> Get(int id, CancellationToken cancellationToken) =>
        await employees.GetAsync(id, cancellationToken);

    /// <summary>Creates a staff account with an initial password.</summary>
    /// <param name="request">Name, e-mail, role and initial password.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="201">Created; Location points to the new account.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="409">E-mail already used (code auth.email_taken).</response>
    [HttpPost]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<EmployeeResponse>> Create(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken
    )
    {
        var created = await employees.CreateAsync(request, cancellationToken);

        // 201 + "Location: /api/employees/{id}".
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Changes name, e-mail and role of a staff account.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="request">All editable fields.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="200">The updated account.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No employee with this id.</response>
    /// <response code="409">E-mail already used (code auth.email_taken).</response>
    /// <response code="422">Own role or last administrator (codes employees.self_change, employees.last_admin).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<EmployeeResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<EmployeeResponse>> Update(
        int id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken
    ) => await employees.UpdateAsync(id, request, cancellationToken);

    /// <summary>Deactivates a staff account and ends all its sessions.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deactivated (or already inactive).</response>
    /// <response code="404">No employee with this id.</response>
    /// <response code="422">Own account or last administrator.</response>
    [HttpPost("{id:int}/deactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        await employees.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Activates a deactivated staff account.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Active.</response>
    /// <response code="404">No employee with this id.</response>
    [HttpPost("{id:int}/activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken)
    {
        await employees.ActivateAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Sets a new password and ends all sessions of the account.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="request">The new password.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Password changed.</response>
    /// <response code="400">The password breaks the policy.</response>
    /// <response code="404">No employee with this id.</response>
    [HttpPost("{id:int}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ResetPassword(
        int id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken
    )
    {
        await employees.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Deletes a staff account that nothing references (otherwise: deactivate it).</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancelled when the client disconnects.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="404">No employee with this id.</response>
    /// <response code="409">The employee recorded payments (code conflict.in_use) – deactivate instead.</response>
    /// <response code="422">Own account or last administrator.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await employees.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
