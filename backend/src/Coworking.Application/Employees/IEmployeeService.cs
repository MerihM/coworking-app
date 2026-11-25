using Coworking.Application.Common.Pagination;
using Coworking.Application.Employees.Dtos;

namespace Coworking.Application.Employees;

/// <summary>
/// Staff account management for the administrator (FR12, UC14): list, view, create, edit,
/// deactivate/activate, reset password and delete (instrukcije.txt: input form, list, edit form, delete).
/// </summary>
public interface IEmployeeService
{
    /// <summary>One page of employees, filtered and sorted by last name, first name.</summary>
    /// <param name="query">Paging and filters.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task<PagedResult<EmployeeResponse>> ListAsync(ListEmployeesQuery query, CancellationToken cancellationToken = default);

    /// <summary>One employee.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.NotFoundException">No such employee (404).</exception>
    Task<EmployeeResponse> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a staff account with an initial password.</summary>
    /// <param name="request">The new account.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.ConflictException">E-mail already used (409, auth.email_taken).</exception>
    Task<EmployeeResponse> CreateAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Changes name, e-mail and role.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="request">The new values.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <exception cref="Coworking.Application.Common.Exceptions.BusinessRuleException">Own role or last administrator (422).</exception>
    Task<EmployeeResponse> UpdateAsync(int id, UpdateEmployeeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deactivates the account and ends all its sessions. Already inactive = no change.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Activates the account again.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task ActivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password and ends all sessions of the account.</summary>
    /// <param name="id">Employee id.</param>
    /// <param name="request">The new password.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the account. Works only when nothing references it; an employee who recorded
    /// payments must be deactivated instead (409 conflict.in_use from the database).
    /// </summary>
    /// <param name="id">Employee id.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
