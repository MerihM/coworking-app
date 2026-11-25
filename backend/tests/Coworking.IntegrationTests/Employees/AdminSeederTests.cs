using System.Net;
using System.Net.Http.Json;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Coworking.Infrastructure.Seeding;
using Coworking.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Employees;

/// <summary>
/// The first administrator (CW-12). Its own test class = its own database container, because
/// these tests switch off every other administrator.
/// </summary>
public class AdminSeederTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Seed_NoActiveAdministrator_CreatesOne_ThatCanLogIn()
    {
        await DeactivateAllAdministratorsAsync();
        var options = NewOptions();

        var created = await AdminSeeder.SeedAsync(factory.Services, options);

        Assert.True(created);

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(options.Email!, options.Password!)
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(Roles.Administrator, session!.User.Role);
    }

    [Fact]
    public async Task Seed_RunTwice_CreatesOnlyOneAdministrator()
    {
        await DeactivateAllAdministratorsAsync();

        var first = await AdminSeeder.SeedAsync(factory.Services, NewOptions());
        var second = await AdminSeeder.SeedAsync(factory.Services, NewOptions());

        Assert.True(first);
        Assert.False(second); // an active administrator exists now → nothing to do
        Assert.Equal(1, await CountActiveAdministratorsAsync());
    }

    [Fact]
    public async Task Seed_PasswordTooShort_CreatesNothing()
    {
        await DeactivateAllAdministratorsAsync();
        var options = NewOptions();
        options.Password = "short";

        var created = await AdminSeeder.SeedAsync(factory.Services, options);

        Assert.False(created);
        Assert.Equal(0, await CountActiveAdministratorsAsync());
    }

    [Fact]
    public async Task SeedAdministrator_WithoutConfiguration_DoesNothing()
    {
        // The test configuration has no Seed:Admin section – exactly like CI and integration tests.
        var created = await factory.Services.SeedAdministratorAsync();

        Assert.False(created);
    }

    private static SeedAdminOptions NewOptions() =>
        new() { Email = $"admin-{Guid.NewGuid():N}@example.com", Password = "Admin12345" };

    private async Task DeactivateAllAdministratorsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        await db
            .Employees.Where(e => e.Role == EmployeeRole.Administrator)
            .ExecuteUpdateAsync(setters => setters.SetProperty(e => e.IsActive, false));
    }

    private async Task<int> CountActiveAdministratorsAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        return await db.Employees.CountAsync(e => e.IsActive && e.Role == EmployeeRole.Administrator);
    }
}
