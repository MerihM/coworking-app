using System.Security.Cryptography;
using System.Text;
using Coworking.Application.Auth;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Auth;
using Coworking.Infrastructure.Persistence;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Coworking.IntegrationTests.Auth;

/// <summary>
/// RefreshTokenService against a real PostgreSQL 18 (Testcontainers): rotation, reuse
/// detection, expiry, revocation, row locking, cleanup and the table's constraints.
/// Every call to the service uses a NEW scope (= new DbContext), like separate HTTP requests.
/// Each test seeds its own users, so tests don't affect each other.
/// </summary>
public class RefreshTokenServiceTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    /// <summary>Fake "now" for these tests (a Monday morning, UTC).</summary>
    private static readonly DateTimeOffset Start = new(2030, 1, 7, 8, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Lifetime = new RefreshTokenOptions().Lifetime;

    [Fact]
    public async Task Issue_StoresOnlyTheHashOfTheToken()
    {
        var user = await SeedMemberAsync();

        var issued = await UseServiceAsync(new FakeTimeProvider(Start), s => s.IssueAsync(user));

        var stored = await QueryAsync(db => db.RefreshTokens.AsNoTracking().SingleAsync(t => t.MemberId == user.Id));
        Assert.Equal(SHA256.HashData(Encoding.ASCII.GetBytes(issued.Token)), stored.TokenHash);
        Assert.NotEqual(Encoding.ASCII.GetBytes(issued.Token), stored.TokenHash);
        Assert.Equal(Start + Lifetime, issued.ExpiresAt);
        Assert.Equal(Start + Lifetime, stored.ExpiresAt);
    }

    [Fact]
    public async Task Rotate_ActiveToken_ReturnsNewTokenAndCurrentUser()
    {
        var user = await SeedEmployeeAsync(EmployeeRole.Manager);
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));
        time.Advance(TimeSpan.FromMinutes(15));

        var result = await UseServiceAsync(time, s => s.RotateAsync(issued.Token));

        Assert.True(result.Succeeded, result.Failure?.ToString());
        Assert.NotEqual(issued.Token, result.Token.Token);
        Assert.Equal(user, result.User);
        Assert.Equal(time.GetUtcNow() + Lifetime, result.Token.ExpiresAt);
    }

    [Fact]
    public async Task Rotate_SameTokenTwice_DetectsReuseAndRevokesWholeFamily()
    {
        var user = await SeedMemberAsync();
        var time = new FakeTimeProvider(Start);
        var original = await UseServiceAsync(time, s => s.IssueAsync(user));
        var first = await UseServiceAsync(time, s => s.RotateAsync(original.Token));
        Assert.True(first.Succeeded);

        // An attacker (or a buggy client) presents the OLD token again.
        var reused = await UseServiceAsync(time, s => s.RotateAsync(original.Token));

        Assert.Equal(RefreshTokenFailure.ReuseDetected, reused.Failure);

        // The legitimate new token is dead too: the whole session is over.
        var afterReuse = await UseServiceAsync(time, s => s.RotateAsync(first.Token.Token));
        Assert.Equal(RefreshTokenFailure.Revoked, afterReuse.Failure);
    }

    [Fact]
    public async Task Rotate_ParallelRequestsWithSameToken_OnlyOneSucceeds()
    {
        // SELECT ... FOR UPDATE: the second request waits for the first, then sees the token rotated.
        var user = await SeedMemberAsync();
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));

        var results = await Task.WhenAll(
            UseServiceAsync(time, s => s.RotateAsync(issued.Token)),
            UseServiceAsync(time, s => s.RotateAsync(issued.Token))
        );

        Assert.Single(results, r => r.Succeeded);
        Assert.Single(results, r => r.Failure == RefreshTokenFailure.ReuseDetected);
    }

    [Fact]
    public async Task Rotate_OneSecondBeforeExpiry_Succeeds()
    {
        var user = await SeedMemberAsync();
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));
        time.Advance(Lifetime - TimeSpan.FromSeconds(1));

        var result = await UseServiceAsync(time, s => s.RotateAsync(issued.Token));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Rotate_AtExpiry_ReturnsExpired()
    {
        var user = await SeedMemberAsync();
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));
        time.Advance(Lifetime);

        var result = await UseServiceAsync(time, s => s.RotateAsync(issued.Token));

        Assert.Equal(RefreshTokenFailure.Expired, result.Failure);
    }

    [Fact]
    public async Task Rotate_DeactivatedEmployee_ReturnsUserInactiveAndRevokesFamily()
    {
        var user = await SeedEmployeeAsync(EmployeeRole.Receptionist);
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));

        await QueryAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(e => e.Id == user.Id);
            employee.Deactivate();
            return await db.SaveChangesAsync();
        });

        var result = await UseServiceAsync(time, s => s.RotateAsync(issued.Token));

        Assert.Equal(RefreshTokenFailure.UserInactive, result.Failure);
        var stored = await QueryAsync(db => db.RefreshTokens.AsNoTracking().SingleAsync(t => t.EmployeeId == user.Id));
        Assert.NotNull(stored.RevokedAt);
    }

    [Fact]
    public async Task Rotate_RoleChangedSinceLogin_ReturnsNewRole()
    {
        var user = await SeedEmployeeAsync(EmployeeRole.Receptionist);
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));

        await QueryAsync(async db =>
        {
            var employee = await db.Employees.SingleAsync(e => e.Id == user.Id);
            employee.ChangeRole(EmployeeRole.Manager);
            return await db.SaveChangesAsync();
        });

        var result = await UseServiceAsync(time, s => s.RotateAsync(issued.Token));

        Assert.True(result.Succeeded);
        Assert.Equal(Roles.Manager, result.User.Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // well-formed, but never issued
    public async Task Rotate_UnknownOrMalformedToken_ReturnsNotFound(string? token)
    {
        var result = await UseServiceAsync(new FakeTimeProvider(Start), s => s.RotateAsync(token));

        Assert.Equal(RefreshTokenFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task Revoke_ThenRotate_ReturnsRevoked()
    {
        var user = await SeedMemberAsync();
        var time = new FakeTimeProvider(Start);
        var issued = await UseServiceAsync(time, s => s.IssueAsync(user));

        await UseServiceAsync(time, async s =>
        {
            await s.RevokeAsync(issued.Token);
            return true;
        });
        var result = await UseServiceAsync(time, s => s.RotateAsync(issued.Token));

        Assert.Equal(RefreshTokenFailure.Revoked, result.Failure);
    }

    [Fact]
    public async Task RevokeAllForUser_EndsAllSessionsOfThatUserOnly()
    {
        var user = await SeedEmployeeAsync(EmployeeRole.Administrator);
        var otherUser = await SeedEmployeeAsync(EmployeeRole.Administrator);
        var time = new FakeTimeProvider(Start);
        var laptop = await UseServiceAsync(time, s => s.IssueAsync(user));
        var phone = await UseServiceAsync(time, s => s.IssueAsync(user));
        var other = await UseServiceAsync(time, s => s.IssueAsync(otherUser));

        var revoked = await UseServiceAsync(time, s => s.RevokeAllForUserAsync(UserType.Employee, user.Id));

        Assert.Equal(2, revoked);
        Assert.Equal(RefreshTokenFailure.Revoked, (await UseServiceAsync(time, s => s.RotateAsync(laptop.Token))).Failure);
        Assert.Equal(RefreshTokenFailure.Revoked, (await UseServiceAsync(time, s => s.RotateAsync(phone.Token))).Failure);
        Assert.True((await UseServiceAsync(time, s => s.RotateAsync(other.Token))).Succeeded);
    }

    [Fact]
    public async Task Cleanup_DeletesOnlyTokensExpiredLongerThanADay()
    {
        var user = await SeedMemberAsync();
        var time = new FakeTimeProvider(Start);
        var old = await UseServiceAsync(time, s => s.IssueAsync(user));

        // Expired at Start + 7 days; with one day of grace it is deleted after Start + 8 days.
        time.Advance(Lifetime + ExpiredRefreshTokenCleanup.KeepExpiredFor + TimeSpan.FromMinutes(1));
        var fresh = await UseServiceAsync(time, s => s.IssueAsync(user));

        var cleanup = new ExpiredRefreshTokenCleanup(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            time,
            NullLogger<ExpiredRefreshTokenCleanup>.Instance
        );
        await cleanup.CleanupOnceAsync();

        Assert.False(await TokenExistsAsync(old.Token));
        Assert.True(await TokenExistsAsync(fresh.Token));
    }

    [Fact]
    public async Task Database_RejectsTokenWithoutOwner()
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            QueryAsync(db =>
                db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO refresh_tokens (token_hash, family_id, created_at, expires_at)
                    VALUES ({RandomNumberGenerator.GetBytes(32)}, {Guid.NewGuid()}, {Start}, {Start + Lifetime})
                    """
                )
            )
        );

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState); // 23514
        Assert.Equal("ck_refresh_tokens_single_owner", exception.ConstraintName);
    }

    [Fact]
    public async Task Database_RejectsTokenWithTwoOwners()
    {
        var member = await SeedMemberAsync();
        var employee = await SeedEmployeeAsync(EmployeeRole.Receptionist);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            QueryAsync(db =>
                db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO refresh_tokens (token_hash, family_id, member_id, employee_id, created_at, expires_at)
                    VALUES ({RandomNumberGenerator.GetBytes(32)}, {Guid.NewGuid()}, {member.Id}, {employee.Id}, {Start}, {Start + Lifetime})
                    """
                )
            )
        );

        Assert.Equal("ck_refresh_tokens_single_owner", exception.ConstraintName);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>Runs one service call in its own scope/DbContext, with the given clock.</summary>
    private async Task<T> UseServiceAsync<T>(TimeProvider time, Func<RefreshTokenService, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = new RefreshTokenService(
            scope.ServiceProvider.GetRequiredService<CoworkingDbContext>(),
            time,
            Options.Create(new RefreshTokenOptions()),
            NullLogger<RefreshTokenService>.Instance
        );
        return await action(service);
    }

    /// <summary>Runs a database query in its own scope.</summary>
    private async Task<T> QueryAsync<T>(Func<CoworkingDbContext, Task<T>> query)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<CoworkingDbContext>());
    }

    private Task<bool> TokenExistsAsync(string rawToken)
    {
        Assert.True(RefreshTokenCrypto.TryHash(rawToken, out var hash));
        return QueryAsync(db => db.RefreshTokens.AnyAsync(t => t.TokenHash == hash));
    }

    private Task<AuthUser> SeedMemberAsync() =>
        QueryAsync(async db =>
        {
            var unique = Guid.NewGuid().ToString("N")[..8];
            var member = new Member("Test", "Member", $"member-{unique}@example.com", "+387 61 000 000", "hash", Start);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return AuthUser.FromMember(member);
        });

    private Task<AuthUser> SeedEmployeeAsync(EmployeeRole role) =>
        QueryAsync(async db =>
        {
            var unique = Guid.NewGuid().ToString("N")[..8];
            var employee = new Employee("Test", "Employee", $"staff-{unique}@example.com", "hash", role);
            db.Employees.Add(employee);
            await db.SaveChangesAsync();
            return AuthUser.FromEmployee(employee);
        });
}
