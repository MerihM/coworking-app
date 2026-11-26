using System.Net.Http.Headers;
using System.Net.Http.Json;
using Coworking.Application.Auth;
using Coworking.Application.Auth.Dtos;
using Coworking.Application.Common.Abstractions;
using Coworking.Domain.Entities;
using Coworking.Domain.Enums;
using Coworking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.IntegrationTests.Fixtures;

/// <summary>
/// "Give me an HttpClient logged in as a Manager" for tests against a real database.
/// Creates a fresh user with the given role and logs in through POST /api/auth/login, so the
/// token comes from the real login flow (not hand-made). Every call creates a new user.
/// </summary>
public static class AuthenticatedClientFactory
{
    /// <summary>Password of every user created here.</summary>
    public const string Password = "Coworking123";

    /// <summary>Creates a user with <paramref name="role"/>, logs in, returns a client with the Bearer header set.</summary>
    /// <param name="factory">A factory with a database (PostgresApiFactory).</param>
    /// <param name="role">One of <see cref="Roles"/>: Member creates a member, the others an employee.</param>
    public static async Task<AuthenticatedClient> CreateClientAsAsync(
        this WebApplicationFactory<Program> factory,
        string role
    )
    {
        var email = await SeedUserAsync(factory, role);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }
        );

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        login.EnsureSuccessStatusCode();
        var session = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return new AuthenticatedClient(client, session.User);
    }

    private static async Task<string> SeedUserAsync(WebApplicationFactory<Program> factory, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoworkingDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(Password);
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com";

        if (role == Roles.Member)
        {
            db.Members.Add(new Member("Test", role, email, "+387 61 000 000", hash, DateTimeOffset.UtcNow));
        }
        else
        {
            db.Employees.Add(new Employee("Test", role, email, hash, Enum.Parse<EmployeeRole>(role)));
        }

        await db.SaveChangesAsync();
        return email;
    }
}

/// <summary>A logged-in test client plus the user it acts as. Dispose it like an HttpClient.</summary>
/// <param name="Client">HttpClient with "Authorization: Bearer ..." already set.</param>
/// <param name="User">The user from the login response.</param>
public sealed record AuthenticatedClient(HttpClient Client, UserResponse User) : IDisposable
{
    public void Dispose() => Client.Dispose();
}
