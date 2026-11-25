using Coworking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Coworking.Infrastructure.Auth;

/// <summary>
/// Background job that deletes refresh tokens which expired more than a day ago, once a day.
/// Without it the table would grow forever: every refresh (every ~15 minutes per active user)
/// adds a row.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="BackgroundService"/> starts with the API and runs until it stops. It is a
/// singleton, but DbContext is scoped (one per request), so each run creates its own scope.
/// </para>
/// <para>
/// Expired tokens are kept for one extra day so a just-expired, already rotated token can still
/// be recognized as reused. The first run waits a few minutes, so short-lived processes
/// (integration tests, <c>dotnet ef</c>) never touch the database from here.
/// </para>
/// </remarks>
/// <param name="scopeFactory">Creates a DI scope (and with it a DbContext) per run.</param>
/// <param name="timeProvider">Source of "now"; also drives the timer (fake clock in tests).</param>
/// <param name="logger">Logs how many tokens were deleted, and failures.</param>
public sealed class ExpiredRefreshTokenCleanup(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredRefreshTokenCleanup> logger
) : BackgroundService
{
    private static readonly TimeSpan FirstRunDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>How long an expired token is kept before it is deleted.</summary>
    public static readonly TimeSpan KeepExpiredFor = TimeSpan.FromDays(1);

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRunDelay, timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(Interval, timeProvider);
            do
            {
                await CleanupOnceAsync(stoppingToken);
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown (Ctrl+C, docker stop): nothing to do.
        }
    }

    /// <summary>Deletes long-expired tokens once. Public so tests can call it directly.</summary>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>Number of deleted tokens (0 when the run failed).</returns>
    public async Task<int> CleanupOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
            var cutoff = timeProvider.GetUtcNow() - KeepExpiredFor;

            // ExecuteDeleteAsync = one "DELETE FROM refresh_tokens WHERE expires_at < ..." in the database.
            var deleted = await db
                .RefreshTokens.Where(t => t.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation("Deleted {DeletedCount} expired refresh token(s)", deleted);
            }

            return deleted;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A failed cleanup (e.g. database briefly down) must not crash the API: by default an
            // exception escaping a BackgroundService stops the whole host. Try again next day.
            logger.LogWarning(exception, "Refresh token cleanup failed; will retry at the next interval");
            return 0;
        }
    }
}
