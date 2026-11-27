using Coworking.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Common;

/// <summary>
/// E-mail helpers shared by registration (CW-11) and staff management (CW-12).
/// </summary>
public static class EmailAddresses
{
    /// <summary>
    /// Trims and lower-cases an e-mail, exactly like the Member and Employee entities do when they
    /// store it. Lookups must use the same form, or "Amra@Example.com" would not find
    /// "amra@example.com".
    /// </summary>
    /// <param name="email">E-mail as typed by the user.</param>
    /// <returns>The normalized e-mail.</returns>
    public static string Normalize(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Is the e-mail already used by a member OR an employee? Both log in through the same login
    /// form, so an e-mail must be unique across both tables. The database can only enforce
    /// uniqueness per table (unique index on each), so this check lives in the application.
    /// </summary>
    /// <param name="db">The database.</param>
    /// <param name="normalizedEmail">Result of <see cref="Normalize"/>.</param>
    /// <param name="exceptEmployeeId">Ignore this employee (editing an employee keeps their own e-mail).</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns><c>true</c> when someone else already uses the e-mail.</returns>
    public static async Task<bool> IsEmailTakenAsync(
        this IAppDbContext db,
        string normalizedEmail,
        int? exceptEmployeeId = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);

        return await db.Members.AnyAsync(m => m.Email == normalizedEmail, cancellationToken)
            || await db.Employees.AnyAsync(
                e => e.Email == normalizedEmail && e.Id != exceptEmployeeId,
                cancellationToken
            );
    }

    /// <summary>
    /// Same check as <see cref="IsEmailTakenAsync"/>, but ignores one MEMBER: editing a member
    /// (FR08) keeps their own e-mail without a false "already taken".
    /// </summary>
    /// <param name="db">The database.</param>
    /// <param name="normalizedEmail">Result of <see cref="Normalize"/>.</param>
    /// <param name="exceptMemberId">The member being edited.</param>
    /// <param name="cancellationToken">Cancels the queries.</param>
    /// <returns><c>true</c> when another member or any employee already uses the e-mail.</returns>
    public static async Task<bool> IsEmailTakenExceptMemberAsync(
        this IAppDbContext db,
        string normalizedEmail,
        int exceptMemberId,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(db);

        return await db.Members.AnyAsync(
                m => m.Email == normalizedEmail && m.Id != exceptMemberId,
                cancellationToken
            )
            || await db.Employees.AnyAsync(e => e.Email == normalizedEmail, cancellationToken);
    }
}
