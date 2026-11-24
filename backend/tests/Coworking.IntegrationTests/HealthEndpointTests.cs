using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Coworking.IntegrationTests;

/// <summary>
/// Smoke test: the whole API starts in memory and /health answers.
/// WebApplicationFactory runs the real Program.cs (same services, same pipeline),
/// but without opening a network port: requests go straight into the in-memory server.
/// IClassFixture = xUnit creates ONE factory and shares it between all tests in this class.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
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
