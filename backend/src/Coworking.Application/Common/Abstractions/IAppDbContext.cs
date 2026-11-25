using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Common.Abstractions;

/// <summary>
/// The database as the Application layer sees it: the tables that use-case services work with,
/// plus <see cref="SaveChangesAsync"/>. Implemented by <c>CoworkingDbContext</c> in Infrastructure.
/// </summary>
/// <remarks>
/// <para>
/// Why an interface: services (AuthService now, reservations and memberships later) contain
/// business logic, so they belong in Application. Application must not depend on Infrastructure,
/// so it gets this interface instead of the concrete DbContext (dependency inversion).
/// </para>
/// <para>
/// Trade-off: Application now references the EF Core package (for <see cref="DbSet{TEntity}"/> and
/// async LINQ like <c>SingleOrDefaultAsync</c>), but NOT Npgsql or anything PostgreSQL-specific.
/// Writing a separate repository per entity would hide EF completely, but it would mostly
/// duplicate what DbSet already offers. Technical tables (refresh_tokens) are intentionally not
/// listed here: only the Infrastructure services that own them touch them.
/// </para>
/// </remarks>
public interface IAppDbContext
{
    /// <summary>Members (table members).</summary>
    DbSet<Member> Members { get; }

    /// <summary>Employees (table employees).</summary>
    DbSet<Employee> Employees { get; }

    /// <summary>Rooms (table rooms).</summary>
    DbSet<Room> Rooms { get; }

    /// <summary>Workspaces (table workspaces).</summary>
    DbSet<Workspace> Workspaces { get; }

    /// <summary>Reservations (table reservations).</summary>
    DbSet<Reservation> Reservations { get; }

    /// <summary>Membership types (table membership_types).</summary>
    DbSet<MembershipType> MembershipTypes { get; }

    /// <summary>Purchased memberships (table memberships).</summary>
    DbSet<Membership> Memberships { get; }

    /// <summary>Payments (table payments).</summary>
    DbSet<Payment> Payments { get; }

    /// <summary>Writes all tracked changes to the database in one transaction.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>Number of affected rows.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
