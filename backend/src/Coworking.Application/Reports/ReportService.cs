using Coworking.Application.Common.Abstractions;
using Coworking.Application.Reports.Dtos;
using Coworking.Domain.Common;
using Coworking.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Reports;

/// <summary>
/// Implements <see cref="IReportService"/>: the occupancy and revenue report of UC13 (FR11).
/// </summary>
/// <remarks>
/// <para>
/// Two small queries load only the columns the report needs (reservations overlapping the period,
/// payments inside it); grouping and the arithmetic happen in memory with <see cref="OccupancyCalculator"/>.
/// Even a whole year of the demo center is a few thousand rows, and in-memory grouping keeps the
/// half-open overlap math in ONE tested place instead of in SQL.
/// </para>
/// <para>
/// Nothing is written: the report is calculated from the existing reservations and payments every
/// time (design document, Class diagram: "Izvještaj nije klasa").
/// </para>
/// </remarks>
/// <param name="db">The database.</param>
/// <param name="time">The clock, for <see cref="OccupancyRevenueReport.GeneratedAt"/>.</param>
/// <param name="validator">Shape of the period.</param>
public sealed class ReportService(IAppDbContext db, TimeProvider time, IValidator<ReportPeriodQuery> validator)
    : IReportService
{
    /// <inheritdoc />
    public async Task<OccupancyRevenueReport> GetOccupancyRevenueAsync(
        ReportPeriodQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await validator.ValidateAndThrowAsync(query, cancellationToken); // 400 with field errors

        // The validator guarantees both values; the pattern gives them non-nullable names.
        if (query is not { From: { } from, To: { } to })
        {
            throw new InvalidOperationException("The validator lets only complete periods through.");
        }

        var (start, end) = OccupancyCalculator.PeriodBounds(from, to);

        var occupancy = await BuildOccupancyAsync(from, to, start, end, cancellationToken);
        var revenue = await BuildRevenueAsync(start, end, cancellationToken);

        return new OccupancyRevenueReport(from, to, time.GetUtcNow(), occupancy, revenue);
    }

    /// <summary>Occupancy totals and the three breakdowns (type, room, workspace).</summary>
    private async Task<OccupancyReport> BuildOccupancyAsync(
        DateOnly from,
        DateOnly to,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken
    )
    {
        // Every reservation (any status) that overlaps the period – the same overlap test as BR01.
        var reservations = await db
            .Reservations.AsNoTracking()
            .Where(r => r.StartsAt < end && r.EndsAt > start)
            .Select(r => new ReservationSlice(r.WorkspaceId, r.StartsAt, r.EndsAt, r.Status))
            .ToListAsync(cancellationToken);

        var reservedWorkspaceIds = reservations.Select(r => r.WorkspaceId).Distinct().ToList();

        // Counted workspaces: everything reservable (fixed desks are assigned, never reserved) that
        // is offered now OR had a reservation in the period – so a desk deactivated yesterday still
        // shows the hours it was used last month.
        var workspaces = await db
            .Workspaces.AsNoTracking()
            .Where(w =>
                w.Type != WorkspaceType.FixedDesk
                && ((w.IsActive && w.Room.IsActive) || reservedWorkspaceIds.Contains(w.Id))
            )
            .Select(w => new WorkspaceInfo(w.Id, w.Code, w.Type, w.RoomId, w.Room.Name, w.Room.Floor))
            .ToListAsync(cancellationToken);

        // The opening hours are the same for every workspace, so they are calculated once.
        var openHours = OccupancyCalculator.OpenHours(from, to);
        var byWorkspace = reservations.ToLookup(r => r.WorkspaceId);

        var rows = workspaces
            .Select(w =>
            {
                var active = byWorkspace[w.Id].Where(r => r.Status != ReservationStatus.Cancelled).ToList();
                return new WorkspaceRow(
                    w,
                    active.Sum(r => OccupancyCalculator.OverlapHours(r.StartsAt, r.EndsAt, start, end)),
                    active.Count,
                    byWorkspace[w.Id].Count(r => r.Status == ReservationStatus.Cancelled)
                );
            })
            .ToList();

        var byType = rows.GroupBy(r => r.Workspace.Type)
            .OrderBy(g => g.Key) // enum order: HotDesk, Office, MeetingRoom
            .Select(g =>
            {
                var (available, reserved, count) = Totals(g.ToList(), openHours);
                return new OccupancyByType(
                    g.Key,
                    g.Count(),
                    available,
                    reserved,
                    OccupancyCalculator.Rate(reserved, available),
                    count
                );
            })
            .ToList();

        var byRoom = rows.GroupBy(r => (r.Workspace.RoomId, r.Workspace.RoomName, r.Workspace.Floor))
            .OrderBy(g => g.Key.Floor)
            .ThenBy(g => g.Key.RoomName, StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var (available, reserved, count) = Totals(g.ToList(), openHours);
                return new OccupancyByRoom(
                    g.Key.RoomId,
                    g.Key.RoomName,
                    g.Count(),
                    available,
                    reserved,
                    OccupancyCalculator.Rate(reserved, available),
                    count
                );
            })
            .ToList();

        var perWorkspace = rows.Select(r => new WorkspaceOccupancy(
                r.Workspace.Id,
                r.Workspace.Code,
                r.Workspace.Type,
                r.Workspace.RoomName,
                openHours,
                r.ReservedHours,
                OccupancyCalculator.Rate(r.ReservedHours, openHours),
                r.ReservationCount
            ))
            .OrderByDescending(w => w.OccupancyRate)
            .ThenByDescending(w => w.ReservedHours)
            .ThenBy(w => w.Code, StringComparer.Ordinal)
            .ToList();

        var (totalAvailable, totalReserved, totalCount) = Totals(rows, openHours);
        return new OccupancyReport(
            totalAvailable,
            totalReserved,
            OccupancyCalculator.Rate(totalReserved, totalAvailable),
            totalCount,
            rows.Sum(r => r.CancelledCount),
            byType,
            byRoom,
            perWorkspace
        );
    }

    /// <summary>Revenue totals and the breakdowns by membership type, method and day.</summary>
    private async Task<RevenueReport> BuildRevenueAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken
    )
    {
        // Half-open like the occupancy: a payment at exactly midnight belongs to the new day.
        var payments = await db
            .Payments.AsNoTracking()
            .Where(p => p.PaidAt >= start && p.PaidAt < end)
            .Select(p => new PaymentSlice(
                p.PaidAt,
                p.Amount,
                p.Method,
                p.Membership.MembershipTypeId,
                p.Membership.MembershipType.Name
            ))
            .ToListAsync(cancellationToken);

        var byType = payments
            .GroupBy(p => (p.MembershipTypeId, p.TypeName))
            .Select(g => new RevenueByMembershipType(g.Key.MembershipTypeId, g.Key.TypeName, g.Count(), g.Sum(p => p.Amount)))
            .OrderByDescending(t => t.Total)
            .ThenBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        // Both methods always appear (0 when unused), so the UI table has a stable shape.
        var byMethod = Enum.GetValues<PaymentMethod>()
            .Select(method =>
            {
                var own = payments.Where(p => p.Method == method).ToList();
                return new RevenueByMethod(method, own.Count, own.Sum(p => p.Amount));
            })
            .ToList();

        // Grouped by the LOCAL day: a payment at 00:30 Sarajevo time is 23:30 UTC of the day before.
        var byDay = payments
            .GroupBy(p => CenterTimeZone.ToLocalDate(p.PaidAt))
            .OrderBy(g => g.Key)
            .Select(g => new RevenueByDay(g.Key, g.Count(), g.Sum(p => p.Amount)))
            .ToList();

        return new RevenueReport(payments.Sum(p => p.Amount), payments.Count, byType, byMethod, byDay);
    }

    /// <summary>Available hours, reserved hours and reservation count of a group of workspaces.</summary>
    private static (decimal Available, decimal Reserved, int Count) Totals(
        IReadOnlyCollection<WorkspaceRow> rows,
        decimal openHours
    ) => (openHours * rows.Count, rows.Sum(r => r.ReservedHours), rows.Sum(r => r.ReservationCount));

    /// <summary>The columns of a reservation the report needs.</summary>
    private sealed record ReservationSlice(
        int WorkspaceId,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        ReservationStatus Status
    );

    /// <summary>The columns of a workspace (and its room) the report needs.</summary>
    private sealed record WorkspaceInfo(int Id, string Code, WorkspaceType Type, int RoomId, string RoomName, int Floor);

    /// <summary>One workspace with its results for the period.</summary>
    private sealed record WorkspaceRow(
        WorkspaceInfo Workspace,
        decimal ReservedHours,
        int ReservationCount,
        int CancelledCount
    );

    /// <summary>The columns of a payment the report needs.</summary>
    private sealed record PaymentSlice(
        DateTimeOffset PaidAt,
        decimal Amount,
        PaymentMethod Method,
        int MembershipTypeId,
        string TypeName
    );
}
