using Coworking.Application.Availability.Dtos;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Localization;
using Coworking.Domain.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Availability;

/// <summary>Implements <see cref="IAvailabilityService"/>: validate → time zone → BR04 → past → one SQL query.</summary>
/// <param name="db">The database.</param>
/// <param name="validator">Shape rules for the query.</param>
/// <param name="time">Current time (the "already ended" check).</param>
public sealed class AvailabilityService(
    IAppDbContext db,
    IValidator<AvailabilityQuery> validator,
    TimeProvider time
) : IAvailabilityService
{
    /// <inheritdoc />
    public async Task<AvailabilityResponse> SearchAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        // The validator guarantees these three are present.
        var date = query.Date!.Value;
        var from = query.From!.Value;
        var to = query.To!.Value;

        // Local Sarajevo clock readings → UTC instants (ADR 0002). FromLocal picks +01:00 or +02:00
        // for THAT date, so a search across the summer-time switch still gets the right hours.
        var startsAt = CenterTimeZone.FromLocal(date, from);
        var endsAt = CenterTimeZone.FromLocal(date, to);

        // BR04 – the very same check Reservation.Create runs, so the search never shows a slot that
        // booking would refuse. Throws DomainException(ruleCode "BR04") → 422.
        WorkingHours.EnsureWithin(startsAt, endsAt);

        // A period that has already started is still searchable (walk-ins, "until 18:00"); only a
        // period that has completely ended is refused.
        if (endsAt <= time.GetUtcNow())
        {
            throw new BusinessRuleException(
                null,
                Localized.Text(
                    "This time has already passed. Choose a time in the future.",
                    "Ovaj termin je već prošao. Odaberite termin u budućnosti."
                ),
                AvailabilityErrorCodes.InPast
            );
        }

        var workspaces = db.Workspaces.AsNoTracking().Offered().FreeBetween(db.Reservations, startsAt, endsAt);

        if (query.Type is { } type)
        {
            workspaces = workspaces.Where(w => w.Type == type);
        }

        if (query.MinCapacity is { } minCapacity)
        {
            workspaces = workspaces.Where(w => w.Capacity >= minCapacity);
        }

        // ONE query returning flat rows (workspace + its room's columns), already in display order.
        // Grouping into rooms happens in memory below – cheaper and simpler than asking EF for a
        // nested result, and the list is small (a center has dozens of workspaces, not thousands).
        var rows = await workspaces
            .OrderBy(w => w.Room.Floor)
            .ThenBy(w => w.Room.Name)
            .ThenBy(w => w.RoomId)
            .ThenBy(w => w.Code)
            .Select(w => new
            {
                w.Id,
                w.Code,
                w.Type,
                w.Capacity,
                w.RoomId,
                RoomName = w.Room.Name,
                RoomFloor = w.Room.Floor,
                RoomDescription = w.Room.Description,
            })
            .ToListAsync(cancellationToken);

        // GroupBy keeps the order of first appearance, so rooms stay sorted by floor and name.
        var rooms = rows.GroupBy(r => r.RoomId)
            .Select(g =>
            {
                var first = g.First();
                return new AvailableRoom(
                    first.RoomId,
                    first.RoomName,
                    first.RoomFloor,
                    first.RoomDescription,
                    [.. g.Select(r => new AvailableWorkspace(r.Id, r.Code, r.Type, r.Capacity))]
                );
            })
            .ToList();

        return new AvailabilityResponse(date, from, to, startsAt, endsAt, rows.Count, rooms);
    }
}
