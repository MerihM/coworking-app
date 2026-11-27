using Coworking.Application.Auth.Validators;
using Coworking.Application.Availability;
using Coworking.Application.Common;
using Coworking.Application.Common.Abstractions;
using Coworking.Domain.Common;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coworking.Infrastructure.Seeding;

/// <summary>What one seeding run inserted (all zero = nothing to do).</summary>
/// <param name="Rooms">Rooms inserted.</param>
/// <param name="Workspaces">Workspaces inserted.</param>
/// <param name="MembershipTypes">Membership types inserted.</param>
/// <param name="Accounts">Demo accounts inserted.</param>
public sealed record DemoDataSeedResult(int Rooms, int Workspaces, int MembershipTypes, int Accounts)
{
    /// <summary>Paid demo memberships inserted (0 or 1, CW-26). Not positional, so existing callers keep compiling.</summary>
    public int Memberships { get; init; }

    /// <summary>Upcoming demo reservations inserted (0–2, CW-29).</summary>
    public int Reservations { get; init; }

    /// <summary>History members inserted (0 or all of <see cref="DemoData.HistoryMembers"/>, CW-32).</summary>
    public int HistoryMembers { get; init; }

    /// <summary>Past payments of the history members (CW-32).</summary>
    public int HistoryPayments { get; init; }

    /// <summary>Past reservations of the history members, cancelled ones included (CW-32).</summary>
    public int HistoryReservations { get; init; }

    /// <summary>Nothing was inserted.</summary>
    public static DemoDataSeedResult Nothing { get; } = new(0, 0, 0, 0);
}

/// <summary>
/// Fills an EMPTY development database with the demo center from <see cref="DemoData"/> (CW-22),
/// so the UI has something to show and the final pg_dump (Sprint 6) contains realistic data.
/// </summary>
/// <remarks>
/// <para>
/// Runs at startup after <see cref="AdminSeeder"/>, only when <c>Seed:DemoData</c> is true.
/// Idempotent per block, so it is safe on every start:
/// rooms + workspaces only if the rooms table is empty; membership types only if that table is
/// empty; each account only if its e-mail is free (in members AND employees).
/// "Table empty" instead of "this exact row missing" is deliberate: once the manager has edited
/// the rooms, a restart must not bring back a room they deleted.
/// </para>
/// <para>
/// Same safety rules as AdminSeeder: when switched off it doesn't touch the database (tests and
/// <c>dotnet ef</c> start the app before migrating), and a database error is logged, not thrown.
/// </para>
/// </remarks>
public static class DemoDataSeeder
{
    private const string LoggerName = "Coworking.Infrastructure.Seeding.DemoDataSeeder";

    /// <summary>Reads the <c>Seed</c> section and seeds when <c>Seed:DemoData</c> is true.</summary>
    /// <param name="services">The app's root service provider (<c>app.Services</c>).</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>What was inserted.</returns>
    public static Task<DemoDataSeedResult> SeedDemoDataAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var options =
            services.GetRequiredService<IConfiguration>().GetSection(DemoDataOptions.SectionName).Get<DemoDataOptions>()
            ?? new DemoDataOptions();

        if (!options.DemoData)
        {
            // Debug, not Information: in production this is the normal case, not news.
            services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(LoggerName)
                .LogDebug("Seed:DemoData is off; skipping demo data");
            return Task.FromResult(DemoDataSeedResult.Nothing);
        }

        return SeedAsync(services, options, cancellationToken);
    }

    /// <summary>Inserts every block whose table is still empty. Public so tests can pass options directly.</summary>
    /// <param name="services">A service provider with the DbContext and password hasher.</param>
    /// <param name="options">Demo settings (the flag itself is not checked here).</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>What was inserted.</returns>
    public static async Task<DemoDataSeedResult> SeedAsync(
        IServiceProvider services,
        DemoDataOptions options,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerName);

        try
        {
            var (rooms, workspaces) = await SeedRoomsAsync(db, logger, cancellationToken);
            var types = await SeedMembershipTypesAsync(db, logger, cancellationToken);
            var accounts = await SeedAccountsAsync(db, passwordHasher, time, options, logger, cancellationToken);
            var memberships = await SeedDemoMembershipAsync(db, time, logger, cancellationToken);
            var reservations = await SeedDemoReservationsAsync(db, time, logger, cancellationToken);
            var history = await SeedHistoryAsync(db, passwordHasher, time, options, logger, cancellationToken);

            return new DemoDataSeedResult(rooms, workspaces, types, accounts)
            {
                Memberships = memberships,
                Reservations = reservations,
                HistoryMembers = history.Members,
                HistoryPayments = history.Payments,
                HistoryReservations = history.Reservations,
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Demo data seeding failed");
            return DemoDataSeedResult.Nothing;
        }
    }

    private static async Task<(int Rooms, int Workspaces)> SeedRoomsAsync(
        CoworkingDbContext db,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        if (await db.Rooms.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Rooms already exist; skipping demo rooms and workspaces");
            return (0, 0);
        }

        foreach (var seed in DemoData.Rooms)
        {
            // Through the domain, so every rule (code format, capacity per type) applies to demo
            // data too – a typo in DemoData fails loudly instead of creating invalid rows.
            var room = new Room(seed.Name, seed.Floor, seed.Description);
            foreach (var (code, type, capacity) in seed.Workspaces)
            {
                room.AddWorkspace(code, type, capacity);
            }

            db.Rooms.Add(room);
        }

        await db.SaveChangesAsync(cancellationToken);

        var workspaceCount = DemoData.Rooms.Sum(r => r.Workspaces.Count);
        logger.LogInformation(
            "Seeded {RoomCount} demo rooms with {WorkspaceCount} workspaces",
            DemoData.Rooms.Count,
            workspaceCount
        );
        return (DemoData.Rooms.Count, workspaceCount);
    }

    private static async Task<int> SeedMembershipTypesAsync(
        CoworkingDbContext db,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        if (await db.MembershipTypes.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Membership types already exist; skipping the demo price list");
            return 0;
        }

        foreach (var seed in DemoData.MembershipTypes)
        {
            var type = new MembershipType(seed.Name, seed.DurationDays, seed.Price);
            if (!seed.IsActive)
            {
                type.Deactivate();
            }

            db.MembershipTypes.Add(type);
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} demo membership types", DemoData.MembershipTypes.Count);
        return DemoData.MembershipTypes.Count;
    }

    private static async Task<int> SeedAccountsAsync(
        CoworkingDbContext db,
        IPasswordHasher passwordHasher,
        TimeProvider time,
        DemoDataOptions options,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        if (options.DemoPassword is not { Length: >= PasswordRules.MinLength })
        {
            logger.LogWarning(
                "Seed:DemoPassword is missing or shorter than {MinLength} characters; skipping demo accounts",
                PasswordRules.MinLength
            );
            return 0;
        }

        var created = 0;

        foreach (var seed in DemoData.Accounts)
        {
            var email = EmailAddresses.Normalize(seed.Email);
            if (await db.IsEmailTakenAsync(email, cancellationToken: cancellationToken))
            {
                // E.g. the developer already created manager@coworking.ba by hand: keep theirs.
                logger.LogInformation("Account {Email} already exists; not touching it", email);
                continue;
            }

            // A separate hash per account (each gets its own random salt), exactly like accounts
            // created through the API – even though the three demo accounts share one password.
            var hash = passwordHasher.Hash(options.DemoPassword);

            if (seed.Role is { } role)
            {
                db.Employees.Add(new Employee(seed.FirstName, seed.LastName, email, hash, role));
            }
            else
            {
                db.Members.Add(
                    new Member(seed.FirstName, seed.LastName, email, DemoData.MemberPhone, hash, time.GetUtcNow())
                );
            }

            created++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Never log the password (NFR01) – only how many accounts were created.
        logger.LogInformation("Seeded {Count} demo accounts", created);
        return created;
    }

    /// <summary>
    /// Gives the demo member a PAID membership of <see cref="DemoData.DemoMembershipTypeName"/>,
    /// recorded by the demo receptionist (CW-26). Goes through the domain (PurchaseMembership +
    /// RecordPayment), so BR05 sets the period exactly like a real payment at the desk.
    /// </summary>
    /// <remarks>
    /// Runs only when the demo member has NO memberships at all: once they bought or were sold
    /// anything (by hand, while testing), a restart must not add another one.
    /// </remarks>
    private static async Task<int> SeedDemoMembershipAsync(
        CoworkingDbContext db,
        TimeProvider time,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var memberEmail = DemoData.Accounts.Single(a => a.Role is null).Email;
        var receptionistEmail = DemoData.Accounts.Single(a => a.Role == EmployeeRole.Receptionist).Email;

        var member = await db
            .Members.Include(m => m.Memberships)
            .SingleOrDefaultAsync(m => m.Email == memberEmail, cancellationToken);
        if (member is null || member.Memberships.Count > 0 || !member.IsActive)
        {
            logger.LogInformation("Demo member missing, inactive or already has memberships; skipping the demo membership");
            return 0;
        }

        var receptionist = await db.Employees.SingleOrDefaultAsync(
            e => e.Email == receptionistEmail && e.IsActive,
            cancellationToken
        );
        var type = await db.MembershipTypes.FirstOrDefaultAsync(
            t => t.Name == DemoData.DemoMembershipTypeName && t.IsActive,
            cancellationToken
        );
        if (receptionist is null || type is null)
        {
            logger.LogInformation("Demo receptionist or the active demo membership type is missing; skipping the demo membership");
            return 0;
        }

        var now = time.GetUtcNow();
        var membership = member.PurchaseMembership(type, now);
        receptionist.RecordPayment(membership, membership.Price, PaymentMethod.Cash, now);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded a paid demo membership ({Type}) for the demo member", type.Name);
        return 1;
    }

    /// <summary>
    /// Gives the demo member the upcoming reservations of <see cref="DemoData.Reservations"/> on the
    /// next weekday (CW-29). Goes through Reservation.Create, so BR04 and BR02 apply, and skips a
    /// workspace that is missing, deactivated or already taken (BR01).
    /// </summary>
    /// <remarks>
    /// Runs only when the demo member has NO reservations at all, so a restart never adds more and
    /// a reservation the developer cancelled while testing doesn't come back.
    /// </remarks>
    private static async Task<int> SeedDemoReservationsAsync(
        CoworkingDbContext db,
        TimeProvider time,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        var memberEmail = DemoData.Accounts.Single(a => a.Role is null).Email;
        var member = await db
            .Members.Include(m => m.Memberships)
            .SingleOrDefaultAsync(m => m.Email == memberEmail && m.IsActive, cancellationToken);
        if (member is null || await db.Reservations.AnyAsync(r => r.MemberId == member.Id, cancellationToken))
        {
            logger.LogInformation("Demo member missing, inactive or already has reservations; skipping demo reservations");
            return 0;
        }

        var now = time.GetUtcNow();

        // The next Monday–Friday after today (Sarajevo), so the reservations are in the future.
        var day = CenterTimeZone.ToLocalDate(now).AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            day = day.AddDays(1);
        }

        if (!member.HasActiveMembershipOn(day))
        {
            logger.LogInformation("The demo member has no active membership on {Day}; skipping demo reservations", day);
            return 0;
        }

        var codes = DemoData.Reservations.Select(r => r.WorkspaceCode).ToList();
        var workspaces = await db
            .Workspaces.Include(w => w.Room)
            .Where(w => codes.Contains(w.Code))
            .ToListAsync(cancellationToken);

        var created = 0;
        foreach (var seed in DemoData.Reservations)
        {
            var workspace = workspaces.SingleOrDefault(w => w.Code == seed.WorkspaceCode);
            if (workspace is not { IsActive: true, Room.IsActive: true })
            {
                continue;
            }

            var startsAt = CenterTimeZone.FromLocal(day, seed.From);
            var endsAt = CenterTimeZone.FromLocal(day, seed.To);
            var isFree = await db
                .Workspaces.Where(w => w.Id == workspace.Id)
                .FreeBetween(db.Reservations, startsAt, endsAt)
                .AnyAsync(cancellationToken);
            if (!isFree)
            {
                continue;
            }

            db.Reservations.Add(Reservation.Create(member, workspace, startsAt, endsAt, now));
            created++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} demo reservations for {Day}", created, day);
        return created;
    }
    /// <summary>
    /// Four weeks of past activity for the FR11 report (CW-32): the members of
    /// <see cref="DemoData.HistoryMembers"/>, their paid memberships and their past reservations,
    /// following <see cref="DemoHistory.Plan"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything still goes through the domain – PurchaseMembership, Employee.RecordPayment (BR05),
    /// Reservation.Create (BR04, BR02) and CancelByMember (BR03) – only with a "now" in the past:
    /// on the visit day the member pays at 07:00, reserves at 07:05 and (in every second week)
    /// cancels at 07:10. So the history obeys the same rules as live data, and Completed is derived
    /// from the end time as usual.
    /// </para>
    /// <para>
    /// Runs only when none of the history e-mails exists yet (a restart adds nothing) and only with
    /// a demo password, like the other demo accounts. A visit whose workspace is missing, inactive
    /// or already taken (BR01, e.g. by the developer's own test data) is skipped, not forced.
    /// </para>
    /// </remarks>
    private static async Task<(int Members, int Payments, int Reservations)> SeedHistoryAsync(
        CoworkingDbContext db,
        IPasswordHasher passwordHasher,
        TimeProvider time,
        DemoDataOptions options,
        ILogger logger,
        CancellationToken cancellationToken
    )
    {
        if (options.DemoPassword is not { Length: >= PasswordRules.MinLength })
        {
            return (0, 0, 0); // the warning was already logged by SeedAccountsAsync
        }

        foreach (var seed in DemoData.HistoryMembers)
        {
            if (await db.IsEmailTakenAsync(EmailAddresses.Normalize(seed.Email), cancellationToken: cancellationToken))
            {
                logger.LogInformation("History member {Email} already exists; skipping the demo history", seed.Email);
                return (0, 0, 0);
            }
        }

        var receptionistEmail = DemoData.Accounts.Single(a => a.Role == EmployeeRole.Receptionist).Email;
        var receptionist = await db.Employees.SingleOrDefaultAsync(
            e => e.Email == receptionistEmail && e.IsActive,
            cancellationToken
        );
        if (receptionist is null)
        {
            logger.LogInformation("The demo receptionist is missing; skipping the demo history");
            return (0, 0, 0);
        }

        // Only active types can be bought (Member.PurchaseMembership), so only those are planned.
        var types = await db.MembershipTypes.Where(t => t.IsActive).ToListAsync(cancellationToken);
        var typesByName = types.GroupBy(t => t.Name).ToDictionary(g => g.Key, g => g.First());

        var codes = DemoData.HistoryMembers.SelectMany(m => m.Visits).Select(v => v.WorkspaceCode).Distinct().ToList();
        var workspacesByCode = await db
            .Workspaces.Include(w => w.Room)
            .Where(w => codes.Contains(w.Code))
            .ToDictionaryAsync(w => w.Code, cancellationToken);

        var today = CenterTimeZone.ToLocalDate(time.GetUtcNow());
        var plan = DemoHistory.Plan(today, typesByName.ToDictionary(p => p.Key, p => p.Value.DurationDays));
        var registeredAt = CenterTimeZone.FromLocal(today.AddDays(-DemoData.HistoryDays - 1), new TimeOnly(12, 0));

        // The members (a separate hash per account, like SeedAccountsAsync).
        var members = new Dictionary<string, Member>();
        foreach (var seed in DemoData.HistoryMembers)
        {
            var member = new Member(
                seed.FirstName,
                seed.LastName,
                EmailAddresses.Normalize(seed.Email),
                seed.Phone,
                passwordHasher.Hash(options.DemoPassword),
                registeredAt
            );
            db.Members.Add(member);
            members[seed.Email] = member;
        }

        // Paid memberships: bought at 06:55, paid at 07:00 on the day (BR05 → starts that day).
        var payments = 0;
        foreach (var purchase in plan.Purchases)
        {
            var paidAt = CenterTimeZone.FromLocal(purchase.Day, new TimeOnly(7, 0));
            var membership = members[purchase.Email]
                .PurchaseMembership(typesByName[purchase.MembershipTypeName], paidAt.AddMinutes(-5));
            receptionist.RecordPayment(membership, membership.Price, purchase.Method, paidAt);
            payments++;
        }

        // Reservations: made at 07:05 on the day; every second week some are cancelled at 07:10.
        var reservations = 0;
        foreach (var visit in plan.Visits)
        {
            var member = members[visit.Email];
            if (
                !workspacesByCode.TryGetValue(visit.WorkspaceCode, out var workspace)
                || workspace is not { IsActive: true, Room.IsActive: true }
                || !member.HasActiveMembershipOn(visit.Day)
            )
            {
                continue;
            }

            var startsAt = CenterTimeZone.FromLocal(visit.Day, visit.From);
            var endsAt = CenterTimeZone.FromLocal(visit.Day, visit.To);
            if (!visit.Cancelled)
            {
                // Rows saved earlier only; the plan itself never overlaps on one workspace.
                var isFree = await db
                    .Workspaces.Where(w => w.Id == workspace.Id)
                    .FreeBetween(db.Reservations, startsAt, endsAt)
                    .AnyAsync(cancellationToken);
                if (!isFree)
                {
                    continue;
                }
            }

            var reservation = Reservation.Create(
                member,
                workspace,
                startsAt,
                endsAt,
                CenterTimeZone.FromLocal(visit.Day, new TimeOnly(7, 5))
            );
            if (visit.Cancelled)
            {
                reservation.CancelByMember(CenterTimeZone.FromLocal(visit.Day, new TimeOnly(7, 10)));
            }

            db.Reservations.Add(reservation);
            reservations++;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Seeded demo history: {Members} members, {Payments} payments, {Reservations} past reservations",
            members.Count,
            payments,
            reservations
        );
        return (members.Count, payments, reservations);
    }
}
