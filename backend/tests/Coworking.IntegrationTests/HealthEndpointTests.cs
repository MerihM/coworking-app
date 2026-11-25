using System.Net;
using Coworking.IntegrationTests.Fixtures;

namespace Coworking.IntegrationTests;

/// <summary>
/// Smoke test: the whole API starts in memory, connects to PostgreSQL, and /health answers "Healthy".
/// Uses PostgresApiFactory because the health check includes the database.
/// IClassFixture = xUnit creates ONE factory (and one database container) and shares it
/// between all tests in this class.
/// </summary>
public class HealthEndpointTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    [Fact]
    public async Task Health_WhenApiIsRunning_ReturnsOkAndHealthy()
    {
        // Arrange: an HttpClient connected to the in-memory API
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
