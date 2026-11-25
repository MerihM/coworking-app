using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Infrastructure.Persistence;

/// <summary>
/// EF Core session with the coworking_db PostgreSQL database.
/// One DbSet per table; table mapping details live in Persistence/Configurations
/// (one IEntityTypeConfiguration class per entity).
/// </summary>
/// <param name="options">Provider and connection string, configured in DependencyInjection.AddInfrastructure.</param>
public sealed class CoworkingDbContext(DbContextOptions<CoworkingDbContext> options)
    : DbContext(options)
{
    /// <summary>Members (table members).</summary>
    public DbSet<Member> Members => Set<Member>();

    /// <summary>Employees (table employees).</summary>
    public DbSet<Employee> Employees => Set<Employee>();

    /// <summary>Rooms (table rooms).</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>Workspaces (table workspaces).</summary>
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    /// <summary>Reservations (table reservations).</summary>
    public DbSet<Reservation> Reservations => Set<Reservation>();

    /// <summary>Membership types (table membership_types).</summary>
    public DbSet<MembershipType> MembershipTypes => Set<MembershipType>();

    /// <summary>Purchased memberships (table memberships).</summary>
    public DbSet<Membership> Memberships => Set<Membership>();

    /// <summary>Payments (table payments).</summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <summary>Builds the model: extensions + all entity configurations from this assembly.</summary>
    /// <param name="modelBuilder">EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // btree_gist lets a GiST index combine "=" on an integer (workspace_id) with "&&" (overlap)
        // on a time range. Needed by the BR01 EXCLUDE constraint. The migration runs
        // CREATE EXTENSION IF NOT EXISTS btree_gist.
        modelBuilder.HasPostgresExtension("btree_gist");

        // Finds every IEntityTypeConfiguration<T> in this project and applies it.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoworkingDbContext).Assembly);
    }

    /// <summary>Conventions applied to ALL entities, so each configuration doesn't have to repeat them.</summary>
    /// <param name="configurationBuilder">EF Core convention builder.</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every enum is stored as its name ('Confirmed'), not as a number (0).
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);

        // Every decimal (money) is numeric(10,2): exact, up to 99,999,999.99 KM.
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }
}
