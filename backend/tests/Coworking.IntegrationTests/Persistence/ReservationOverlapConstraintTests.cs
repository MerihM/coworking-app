using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Coworking.IntegrationTests.Persistence;

/// <summary>
/// BR01 at the DATABASE level: the EXCLUDE constraint must reject overlapping confirmed
/// reservations even when application code doesn't check (e.g. two parallel requests).
/// Each test uses its own workspace, member etc., so tests don't affect each other.
/// </summary>
public class ReservationOverlapConstraintTests(PostgresApiFactory factory)
    : IClassFixture<PostgresApiFactory>
{
    /// <summary>"Now" for these tests; reservations are on Monday 2030-01-07.</summary>
    private static readonly DateTimeOffset Now = At(2030, 1, 1, 8);

    [Fact]
    public async Task BR01_OverlappingConfirmedReservation_IsRejectedByDatabase()
    {
        var (memberId, workspaceId) = await SeedMemberAndWorkspaceAsync();
        await SaveReservationAsync(memberId, workspaceId, fromHour: 10, toHour: 12);

        // 11:00–13:00 overlaps 10:00–12:00 (TC03) and is NOT checked in code here on purpose.
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            SaveReservationAsync(memberId, workspaceId, fromHour: 11, toHour: 13)
        );

        var postgresError = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgresError.SqlState); // 23P01
        Assert.Equal("ex_reservations_no_overlap", postgresError.ConstraintName);
    }

    [Fact]
    public async Task BR01_TouchingReservations_AreAllowedByDatabase()
    {
        var (memberId, workspaceId) = await SeedMemberAndWorkspaceAsync();
        await SaveReservationAsync(memberId, workspaceId, fromHour: 10, toHour: 12);

        // 12:00–14:00 only touches 10:00–12:00 (TC04): '[)' ranges don't overlap.
        var exception = await Record.ExceptionAsync(() =>
            SaveReservationAsync(memberId, workspaceId, fromHour: 12, toHour: 14)
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task BR01_CancelledReservation_DoesNotBlockTheSlot()
    {
        var (memberId, workspaceId) = await SeedMemberAndWorkspaceAsync();
        var reservationId = await SaveReservationAsync(
            memberId,
            workspaceId,
            fromHour: 10,
            toHour: 12
        );

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
            var reservation = await db.Reservations.SingleAsync(r => r.Id == reservationId);
            reservation.CancelByMember(Now);
            await db.SaveChangesAsync();
        }

        // Same slot again: allowed, because the constraint only covers status = 'Confirmed'.
        var exception = await Record.ExceptionAsync(() =>
            SaveReservationAsync(memberId, workspaceId, fromHour: 10, toHour: 12)
        );

        Assert.Null(exception);
    }

    /// <summary>
    /// Saves a room with one workspace and a member with a paid monthly membership.
    /// Unique names/e-mails per test, because those columns have UNIQUE indexes.
    /// </summary>
    private async Task<(int MemberId, int WorkspaceId)> SeedMemberAndWorkspaceAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var room = new Room($"Room {unique}", 1, null);
        var workspace = room.AddWorkspace($"T{unique}", WorkspaceType.HotDesk, 1);

        var member = new Member(
            "Test",
            "Member",
            $"member-{unique}@example.com",
            "+387 61 000 000",
            "hash",
            Now
        );
        var membership = member.PurchaseMembership(
            new MembershipType($"Monthly {unique}", 30, 120m),
            Now
        );
        var receptionist = new Employee(
            "Test",
            "Receptionist",
            $"staff-{unique}@example.com",
            "hash",
            EmployeeRole.Receptionist
        );
        receptionist.RecordPayment(membership, 120m, PaymentMethod.Cash, Now);

        // Adding the roots is enough: EF follows the navigations and inserts the whole graph
        // (workspace, membership, membership type, payment, employee).
        db.AddRange(room, member);
        await db.SaveChangesAsync();

        return (member.Id, workspace.Id);
    }

    /// <summary>
    /// Loads member + workspace in a NEW DbContext (like a real request would) and saves a
    /// reservation on Monday 2030-01-07 between the given hours. Returns the new reservation id.
    /// </summary>
    private async Task<int> SaveReservationAsync(
        int memberId,
        int workspaceId,
        int fromHour,
        int toHour
    )
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();

        var member = await db
            .Members.Include(m => m.Memberships)
            .SingleAsync(m => m.Id == memberId);
        var workspace = await db.Workspaces.SingleAsync(w => w.Id == workspaceId);

        var reservation = Reservation.Create(
            member,
            workspace,
            At(2030, 1, 7, fromHour),
            At(2030, 1, 7, toHour),
            Now
        );

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();
        return reservation.Id;
    }

    private static DateTimeOffset At(int year, int month, int day, int hour) =>
        CenterTimeZone.FromLocal(new DateOnly(year, month, day), new TimeOnly(hour, 0));
}
