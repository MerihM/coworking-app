using Coworking.Application.Auth;
using Coworking.Application.Availability;
using Coworking.Application.Common.Abstractions;
using Coworking.Application.Common.Exceptions;
using Coworking.Application.Common.Localization;
using Coworking.Application.Common.Pagination;
using Coworking.Application.Reservations.Dtos;
using Coworking.Application.Reservations.Validators;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Coworking.Application.Reservations;

/// <summary>
/// Implements <see cref="IReservationService"/> – the ":RezervacijaServis" control object of the
/// Sequence diagram. The rules themselves live in the domain (Reservation.Create: BR04 + BR02;
/// CancelByMember: BR03; CancelByEmployee); this class loads what they need, checks the conditions
/// that need the database (BR01, ownership) and gives each refusal a precise error code.
/// </summary>
/// <param name="db">The database.</param>
/// <param name="currentUser">The logged-in member (mine) or employee (reception).</param>
/// <param name="time">The clock: "now" for past checks, BR03, derived statuses.</param>
/// <param name="createValidator">Shape of a reservation request.</param>
/// <param name="alternativesValidator">Shape of an alternatives query.</param>
/// <param name="mineValidator">Paging/scope of the member's list.</param>
/// <param name="listValidator">Paging/filters of the reception list.</param>
public sealed class ReservationService(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider time,
    IValidator<CreateReservationRequest> createValidator,
    IValidator<ReservationAlternativesQuery> alternativesValidator,
    IValidator<MyReservationsQuery> mineValidator,
    IValidator<ListReservationsQuery> listValidator
) : IReservationService
{
    /// <summary>At most this many other workspaces are offered (UC06).</summary>
    public const int MaxOtherWorkspaces = 5;

    /// <summary>At most this many later slots are offered (UC06).</summary>
    public const int MaxNextSlots = 3;

    /// <summary>Later slots are searched on the requested day and the following days, this many days in total.</summary>
    public const int SlotSearchDays = 7;

    /// <summary>Slots start on a quarter hour, like everything the UI offers.</summary>
    private static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(15);

    /// <inheritdoc />
    public async Task<ReservationResponse> CreateMineAsync(
        CreateReservationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var memberId = CurrentMemberId();
        var now = time.GetUtcNow();

        // UC04 step 3: the chosen workspace must be one the center offers (404 / 422).
        var workspace = await LoadBookableWorkspaceAsync(request.WorkspaceId, cancellationToken);

        // Local Sarajevo clock readings → UTC instants (ADR 0002), the same conversion as the search.
        var startsAt = CenterTimeZone.FromLocal(request.Date, request.From);
        var endsAt = CenterTimeZone.FromLocal(request.Date, request.To);

        // The domain refuses this too, but without a code; checking here gives a translated message.
        if (startsAt <= now)
        {
            throw new BusinessRuleException(
                null,
                Localized.Text(
                    "A reservation must start in the future. Choose a later time.",
                    "Rezervacija mora početi u budućnosti. Odaberite kasniji termin."
                ),
                ReservationErrorCodes.InPast
            );
        }

        // BR02 needs ALL memberships (a paid renewal may cover a later date).
        var member =
            await db.Members.Include(m => m.Memberships).SingleOrDefaultAsync(m => m.Id == memberId, cancellationToken)
            ?? throw new ForbiddenException();

        // Activity diagram, decisions 1 and 2: BR04 (flow 3a) then BR02 (flow 4a).
        // Both throw DomainException with ruleCode "BR04" / "BR02" → 422; nothing is saved.
        var reservation = Reservation.Create(member, workspace, startsAt, endsAt, now);

        // Decision 3: is the workspace free (BR01, "dostupno(pocetak, kraj)")? The SAME predicate the
        // search uses. If two members pass this check at the same moment, the EXCLUDE constraint lets
        // only one INSERT through; the other gets 23P01 → the same 409 (GlobalExceptionHandler).
        var isFree = await db
            .Workspaces.Where(w => w.Id == workspace.Id)
            .FreeBetween(db.Reservations, startsAt, endsAt)
            .AnyAsync(cancellationToken);
        if (!isFree)
        {
            throw new ConflictException(
                Localized.Text(
                    "This workspace is already reserved for that time.",
                    "Ovo radno mjesto je već rezervisano u tom terminu."
                ),
                ruleCode: "BR01",
                code: ReservationErrorCodes.SlotTaken
            );
        }

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync(cancellationToken);

        // UC04 step 6: the confirmation.
        return await GetAsync(reservation.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReservationAlternativesResponse> GetAlternativesAsync(
        ReservationAlternativesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await alternativesValidator.ValidateAndThrowAsync(query, cancellationToken);
        _ = CurrentMemberId(); // only members reserve

        // The validator guarantees these are present.
        var date = query.Date!.Value;
        var from = query.From!.Value;
        var to = query.To!.Value;
        var now = time.GetUtcNow();

        var workspace = await LoadBookableWorkspaceAsync(query.WorkspaceId!.Value, cancellationToken);
        var startsAt = CenterTimeZone.FromLocal(date, from);
        var endsAt = CenterTimeZone.FromLocal(date, to);

        // Offering alternatives for a period that can't be booked anyway makes no sense (422 BR04).
        WorkingHours.EnsureWithin(startsAt, endsAt);

        // Locals instead of entity properties inside the LINQ: EF sends them as plain parameters.
        var workspaceId = workspace.Id;
        var roomId = workspace.RoomId;
        var type = workspace.Type;
        var capacity = workspace.Capacity;

        IReadOnlyList<AlternativeWorkspace> otherWorkspaces = [];
        if (startsAt > now)
        {
            // Same definition of "free" as the search and BR01. Same room first (the member most
            // likely wants to sit there), then by floor, room and code – like the search results.
            otherWorkspaces = await db
                .Workspaces.AsNoTracking()
                .Offered()
                .FreeBetween(db.Reservations, startsAt, endsAt)
                .Where(w => w.Id != workspaceId && w.Type == type && w.Capacity >= capacity)
                .OrderByDescending(w => w.RoomId == roomId)
                .ThenBy(w => w.Room.Floor)
                .ThenBy(w => w.Room.Name)
                .ThenBy(w => w.Code)
                .Take(MaxOtherWorkspaces)
                .Select(w => new AlternativeWorkspace(w.Id, w.Code, w.Type, w.Capacity, w.RoomId, w.Room.Name))
                .ToListAsync(cancellationToken);
        }

        var nextSlots = await FindNextSlotsAsync(workspaceId, date, from, to - from, now, cancellationToken);

        return new ReservationAlternativesResponse(otherWorkspaces, nextSlots);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReservationResponse>> ListMineAsync(
        MyReservationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await mineValidator.ValidateAndThrowAsync(query, cancellationToken);

        var memberId = CurrentMemberId();
        var now = time.GetUtcNow();
        var mine = WithDetails(db.Reservations.AsNoTracking()).Where(r => r.MemberId == memberId);

        // "Upcoming" = would be shown as Confirmed (EffectiveStatusAt); everything else is history.
        var ordered =
            query.Scope == ReservationScope.Past
                ? mine.Where(r => r.Status != ReservationStatus.Confirmed || r.EndsAt <= now)
                    .OrderByDescending(r => r.StartsAt)
                    .ThenByDescending(r => r.Id)
                : mine.Where(r => r.Status == ReservationStatus.Confirmed && r.EndsAt > now)
                    .OrderBy(r => r.StartsAt)
                    .ThenBy(r => r.Id);

        return await ToResponsePageAsync(ordered, query, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task CancelMineAsync(int id, CancellationToken cancellationToken = default)
    {
        var memberId = CurrentMemberId();
        var now = time.GetUtcNow();

        // Filtering by owner in the query: another member's reservation looks exactly like a
        // missing one (404), so ids reveal nothing.
        var reservation =
            await db.Reservations.SingleOrDefaultAsync(r => r.Id == id && r.MemberId == memberId, cancellationToken)
            ?? throw new NotFoundException("Reservation", id);

        EnsureCancellable(reservation, now);

        // BR03: less than 2 h before the start → DomainException ruleCode "BR03" → 422 (TC06).
        reservation.CancelByMember(now);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<ReservationResponse>> ListAsync(
        ListReservationsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);

        var now = time.GetUtcNow();
        var reservations = WithDetails(db.Reservations.AsNoTracking());

        if (query.MemberId is { } memberId)
        {
            reservations = reservations.Where(r => r.MemberId == memberId);
        }

        if (query.Date is { } date)
        {
            // A local day as a half-open UTC range [00:00, next 00:00) – correct on DST days too.
            var dayStart = CenterTimeZone.FromLocal(date, TimeOnly.MinValue);
            var dayEnd = CenterTimeZone.FromLocal(date.AddDays(1), TimeOnly.MinValue);
            reservations = reservations.Where(r => r.StartsAt >= dayStart && r.StartsAt < dayEnd);
        }

        // The SAME meaning as EffectiveStatusAt: "Completed" also covers rows still stored as
        // Confirmed whose period is over.
        reservations = query.Status switch
        {
            ReservationStatus.Confirmed => reservations.Where(r =>
                r.Status == ReservationStatus.Confirmed && r.EndsAt > now
            ),
            ReservationStatus.Completed => reservations.Where(r =>
                r.Status == ReservationStatus.Completed || (r.Status == ReservationStatus.Confirmed && r.EndsAt <= now)
            ),
            ReservationStatus.Cancelled => reservations.Where(r => r.Status == ReservationStatus.Cancelled),
            _ => reservations,
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            reservations = reservations.Where(r =>
                (r.Member.FirstName + " " + r.Member.LastName).ToLower().Contains(term)
                || r.Member.Email.Contains(term)
                || r.Workspace.Code.ToLower().Contains(term)
            );
        }

        var ordered = reservations.OrderBy(r => r.StartsAt).ThenBy(r => r.Id);
        return await ToResponsePageAsync(ordered, query, now, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReservationResponse> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var reservation =
            await WithDetails(db.Reservations.AsNoTracking()).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Reservation", id);

        return ReservationResponse.Map(reservation, time.GetUtcNow());
    }

    /// <inheritdoc />
    public async Task CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var reservation =
            await db.Reservations.SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new NotFoundException("Reservation", id);

        EnsureCancellable(reservation, now);

        var employeeId = CurrentEmployeeId();
        var employee =
            await db.Employees.SingleOrDefaultAsync(e => e.Id == employeeId, cancellationToken)
            ?? throw new ForbiddenException();

        // BR03: reception may cancel after the member's 2-hour deadline too (TC06). The employee is
        // stored on the reservation for the change history.
        reservation.CancelByEmployee(employee, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The workspace (tracked, with its room) if the center offers it for booking. Mirrors
    /// <see cref="WorkspaceAvailabilityQueries.Offered"/>: active, in an active room, not a fixed desk.
    /// </summary>
    private async Task<Workspace> LoadBookableWorkspaceAsync(int workspaceId, CancellationToken cancellationToken)
    {
        var workspace =
            await db.Workspaces.Include(w => w.Room).SingleOrDefaultAsync(w => w.Id == workspaceId, cancellationToken)
            ?? throw new NotFoundException("Workspace", workspaceId);

        if (!workspace.IsActive || !workspace.Room.IsActive || workspace.Type == WorkspaceType.FixedDesk)
        {
            throw new BusinessRuleException(
                null,
                Localized.Text(
                    "This workspace can't be reserved.",
                    "Ovo radno mjesto se ne može rezervisati."
                ),
                ReservationErrorCodes.WorkspaceUnavailable
            );
        }

        return workspace;
    }

    /// <summary>
    /// UC06, "drugi termin": walks the requested day from the requested start, then the following
    /// days from opening time, in 15-minute steps, and collects periods of the same length that are
    /// inside working hours (BR04), in the future and not blocked by a confirmed reservation (BR01).
    /// After a hit it jumps to that slot's end, so the offers are 12–14, 14–16, … rather than
    /// 12:00, 12:15, 12:30.
    /// </summary>
    /// <remarks>
    /// One query loads the workspace's confirmed reservations for the whole week; the walk itself is in
    /// memory (at most ~60 steps a day). BR02 (membership) is not checked here – creating the
    /// reservation still checks it.
    /// </remarks>
    private async Task<IReadOnlyList<AlternativeSlot>> FindNextSlotsAsync(
        int workspaceId,
        DateOnly date,
        TimeOnly from,
        TimeSpan duration,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var windowStart = CenterTimeZone.FromLocal(date, TimeOnly.MinValue);
        var windowEnd = CenterTimeZone.FromLocal(date.AddDays(SlotSearchDays), TimeOnly.MinValue);

        var taken = await db
            .Reservations.AsNoTracking()
            .Where(r =>
                r.WorkspaceId == workspaceId
                && r.Status == ReservationStatus.Confirmed
                && r.StartsAt < windowEnd
                && r.EndsAt > windowStart
            )
            .Select(r => new { r.StartsAt, r.EndsAt })
            .ToListAsync(cancellationToken);

        var lastBookableDay = CenterTimeZone.ToLocalDate(now).AddDays(ReservationSlotRules.MaxDaysAhead);
        var slots = new List<AlternativeSlot>();

        for (var day = date; day < date.AddDays(SlotSearchDays) && day <= lastBookableDay; day = day.AddDays(1))
        {
            if (WorkingHours.GetHours(day.DayOfWeek) is not { } hours)
            {
                continue; // Sunday: closed
            }

            // TimeSpan arithmetic, because TimeOnly + TimeSpan silently wraps around midnight.
            var start = (day == date && from > hours.Open ? from : hours.Open).ToTimeSpan();
            var close = hours.Close.ToTimeSpan();

            while (start + duration <= close)
            {
                var slotFrom = TimeOnly.FromTimeSpan(start);
                var slotTo = TimeOnly.FromTimeSpan(start + duration);
                var slotStartsAt = CenterTimeZone.FromLocal(day, slotFrom);
                var slotEndsAt = CenterTimeZone.FromLocal(day, slotTo);

                // Half-open overlap, exactly like Reservation.OverlapsWith and the EXCLUDE constraint.
                var isFree = !taken.Any(r => r.StartsAt < slotEndsAt && r.EndsAt > slotStartsAt);
                if (slotStartsAt > now && isFree)
                {
                    slots.Add(new AlternativeSlot(day, slotFrom, slotTo, slotStartsAt, slotEndsAt));
                    if (slots.Count == MaxNextSlots)
                    {
                        return slots;
                    }

                    start += duration;
                }
                else
                {
                    start += SlotStep;
                }
            }
        }

        return slots;
    }

    /// <summary>Only a confirmed reservation that hasn't ended can be cancelled – by anyone.</summary>
    private static void EnsureCancellable(Reservation reservation, DateTimeOffset now)
    {
        if (reservation.Status != ReservationStatus.Confirmed || reservation.EndsAt <= now)
        {
            throw new ConflictException(
                Localized.Text(
                    "This reservation is already cancelled or has already ended.",
                    "Ova rezervacija je već otkazana ili je već završena."
                ),
                code: ReservationErrorCodes.NotCancellable
            );
        }
    }

    /// <summary>One page, mapped in memory so the domain rules give the status and BR03 flag.</summary>
    private static async Task<PagedResult<ReservationResponse>> ToResponsePageAsync(
        IQueryable<Reservation> ordered,
        PageQuery paging,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var page = await ordered.ToPagedResultAsync(paging, cancellationToken);
        return new PagedResult<ReservationResponse>(
            [.. page.Items.Select(r => ReservationResponse.Map(r, now))],
            page.Page,
            page.PageSize,
            page.TotalCount
        );
    }

    /// <summary>Member, workspace with its room, and the cancelling employee: everything <see cref="ReservationResponse.Map"/> reads.</summary>
    private static IQueryable<Reservation> WithDetails(IQueryable<Reservation> reservations) =>
        reservations
            .Include(r => r.Member)
            .Include(r => r.Workspace)
            .ThenInclude(w => w.Room)
            .Include(r => r.CancelledByEmployee);

    /// <summary>The MemberOnly policy guarantees a member token; this is the defensive double check.</summary>
    private int CurrentMemberId() =>
        currentUser is { UserType: UserType.Member, Id: { } id } ? id : throw new ForbiddenException();

    /// <summary>The ReceptionistOrManager policy guarantees an employee token.</summary>
    private int CurrentEmployeeId() =>
        currentUser is { UserType: UserType.Employee, Id: { } id } ? id : throw new ForbiddenException();
}
