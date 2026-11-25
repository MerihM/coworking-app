using Coworking.Application.Auth;
using Coworking.Application.Common;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Employees.Dtos;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Employees;

/// <summary>
/// Implements <see cref="IEmployeeService"/>. Who may call it (Administrator only) is decided by
/// the controller's policy; this class enforces the rules about WHAT may be changed.
/// </summary>
/// <remarks>
/// Two safety rules protect the system from losing its administrators:
/// <list type="number">
/// <item>An administrator can't deactivate, delete or change the role of their own account.</item>
/// <item>The last active administrator can't be deactivated, deleted or demoted.</item>
/// </list>
/// Rule 1 normally makes rule 2 unreachable (the caller is an active administrator too), but a
/// deactivated administrator's access token keeps working for up to 15 minutes – rule 2 is the
/// guard for exactly that case.
/// </remarks>
/// <param name="db">The database.</param>
/// <param name="passwordHasher">Hashes initial and reset passwords.</param>
/// <param name="refreshTokens">Ends sessions on deactivation and password reset.</param>
/// <param name="currentUser">The administrator making the change (self-change rule).</param>
/// <param name="listValidator">Rules for the list query.</param>
/// <param name="createValidator">Rules for a new account.</param>
/// <param name="updateValidator">Rules for an edit.</param>
/// <param name="resetPasswordValidator">Rules for a new password.</param>
public sealed class EmployeeService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokens,
    ICurrentUser currentUser,
    IValidator<ListEmployeesQuery> listValidator,
    IValidator<CreateEmployeeRequest> createValidator,
    IValidator<UpdateEmployeeRequest> updateValidator,
    IValidator<ResetPasswordRequest> resetPasswordValidator
) : IEmployeeService
{
    /// <inheritdoc />
    public async Task<PagedResult<EmployeeResponse>> ListAsync(
        ListEmployeesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        // AsNoTracking: read-only list, EF doesn't need to watch the entities for changes.
        var employees = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // ToLower() inside the query becomes SQL lower(...), so the search is case-insensitive.
            // Contains(...) becomes a LIKE/strpos search; EF escapes % and _ typed by the user.
            var term = query.Search.Trim().ToLowerInvariant();
            employees = employees.Where(e =>
                e.FirstName.ToLower().Contains(term)
                || e.LastName.ToLower().Contains(term)
                || (e.FirstName + " " + e.LastName).ToLower().Contains(term)
                || e.Email.Contains(term) // e-mails are stored in lower case already
            );
        }

        if (query.Role is { } role)
        {
            employees = employees.Where(e => e.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            employees = employees.Where(e => e.IsActive == isActive);
        }

        // A stable order is required for paging; Id last makes it unique even for equal names.
        return await employees
            .OrderBy(e => e.LastName)
            .ThenBy(e => e.FirstName)
            .ThenBy(e => e.Id)
            .Select(e => new EmployeeResponse(e.Id, e.FirstName, e.LastName, e.Email, e.Role, e.IsActive))
            .ToPagedResultAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmployeeResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await db
            .Employees.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

        return employee is null
            ? throw new NotFoundException("Employee", id)
            : EmployeeResponse.From(employee);
    }

    /// <inheritdoc />
    public async Task<EmployeeResponse> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var email = EmailAddresses.Normalize(request.Email);
        await EnsureEmailIsFreeAsync(email, exceptEmployeeId: null, cancellationToken);

        var employee = new Employee(
            request.FirstName,
            request.LastName,
            email,
            passwordHasher.Hash(request.Password),
            request.Role
        );

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        return EmployeeResponse.From(employee);
    }

    /// <inheritdoc />
    public async Task<EmployeeResponse> UpdateAsync(
        int id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var employee = await FindAsync(id, cancellationToken);

        if (request.Role != employee.Role)
        {
            EnsureNotSelf(id, "You can't change the role of your own account.");

            // Demoting the last active administrator would leave nobody to manage accounts.
            if (request.Role != EmployeeRole.Administrator)
            {
                await EnsureNotLastActiveAdministratorAsync(employee, cancellationToken);
            }
        }

        var email = EmailAddresses.Normalize(request.Email);
        if (email != employee.Email)
        {
            await EnsureEmailIsFreeAsync(email, exceptEmployeeId: id, cancellationToken);
            employee.ChangeEmail(email);
        }

        employee.UpdateDetails(request.FirstName, request.LastName);
        employee.ChangeRole(request.Role);
        await db.SaveChangesAsync(cancellationToken);

        // No token revocation here: a role change reaches the user at the next refresh (max 15
        // minutes), because RefreshTokenService reloads the role from the database.
        return EmployeeResponse.From(employee);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await FindAsync(id, cancellationToken);
        if (!employee.IsActive)
        {
            return; // Idempotent: deactivating twice is not an error.
        }

        EnsureNotSelf(id, "You can't deactivate your own account.");
        await EnsureNotLastActiveAdministratorAsync(employee, cancellationToken);

        employee.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        // End every session now. The current access token still works until it expires (max
        // 15 min), but the next refresh fails, so the user is logged out.
        await refreshTokens.RevokeAllForUserAsync(UserType.Employee, id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await FindAsync(id, cancellationToken);
        employee.Reactivate();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(
        int id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await resetPasswordValidator.ValidateAndThrowAsync(request, cancellationToken);

        var employee = await FindAsync(id, cancellationToken);
        employee.ChangePasswordHash(passwordHasher.Hash(request.NewPassword));
        await db.SaveChangesAsync(cancellationToken);

        // Whoever knew the old password may already be logged in somewhere: log them out.
        await refreshTokens.RevokeAllForUserAsync(UserType.Employee, id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var employee = await FindAsync(id, cancellationToken);

        EnsureNotSelf(id, "You can't delete your own account.");
        await EnsureNotLastActiveAdministratorAsync(employee, cancellationToken);

        db.Employees.Remove(employee);

        // If payments (or later reservations) reference this employee, the foreign key (ON DELETE
        // RESTRICT) makes PostgreSQL refuse with SqlState 23001 (restrict_violation). GlobalExceptionHandler turns that
        // into 409 "conflict.in_use", and the UI suggests deactivating instead. Letting the
        // database decide means this keeps working when new tables start referencing employees.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Loads a tracked employee (so changes are saved) or throws 404.</summary>
    private async Task<Employee> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Employees.SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
        ?? throw new NotFoundException("Employee", id);

    private async Task EnsureEmailIsFreeAsync(
        string normalizedEmail,
        int? exceptEmployeeId,
        CancellationToken cancellationToken
    )
    {
        if (await db.IsEmailTakenAsync(normalizedEmail, exceptEmployeeId, cancellationToken))
        {
            throw new ConflictException(
                "An account with this e-mail address already exists.",
                code: AuthErrorCodes.EmailTaken
            );
        }
    }

    /// <summary>Rule 1: the caller can't do this to their own account.</summary>
    private void EnsureNotSelf(int employeeId, string message)
    {
        if (currentUser.UserType == UserType.Employee && currentUser.Id == employeeId)
        {
            throw new BusinessRuleException(null, message, EmployeeErrorCodes.SelfChange);
        }
    }

    /// <summary>Rule 2: at least one active administrator must remain.</summary>
    private async Task EnsureNotLastActiveAdministratorAsync(
        Employee employee,
        CancellationToken cancellationToken
    )
    {
        if (!employee.IsActive || employee.Role != EmployeeRole.Administrator)
        {
            return; // Not an active administrator: removing them doesn't change the count.
        }

        var activeAdministrators = await db.Employees.CountAsync(
            e => e.IsActive && e.Role == EmployeeRole.Administrator,
            cancellationToken
        );

        if (activeAdministrators <= 1)
        {
            throw new BusinessRuleException(
                null,
                "At least one active administrator must remain.",
                EmployeeErrorCodes.LastAdministrator
            );
        }
    }
}
