using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace Coworking.IntegrationTests.ErrorHandling;

/// <summary>
/// Checks that every error leaves the API as ProblemDetails (RFC 7807) with the right status code.
/// One API instance (ErrorTestingApiFactory) is shared by all tests in this class.
/// </summary>
public class ErrorHandlingTests(ErrorTestingApiFactory factory)
    : IClassFixture<ErrorTestingApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/test/errors/not-found", HttpStatusCode.NotFound)]
    [InlineData("/test/errors/conflict", HttpStatusCode.Conflict)]
    [InlineData("/test/errors/business-rule", HttpStatusCode.UnprocessableEntity)]
    [InlineData("/test/errors/forbidden", HttpStatusCode.Forbidden)]
    [InlineData("/test/errors/domain-rule", HttpStatusCode.UnprocessableEntity)] // AppException_ReturnsMatching...
    public async Task AppException_ReturnsMatchingStatusAsProblemDetails(
        string url,
        HttpStatusCode expectedStatus
    )
    {
        var response = await _client.GetAsync(url);

        var problem = await AssertProblemDetailsAsync(response, expectedStatus);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
    }

    [Theory]
    [InlineData("/test/errors/conflict", "BR01")]
    [InlineData("/test/errors/business-rule", "BR04")]
    [InlineData("/test/errors/domain-rule", "BR03")] // ExceptionWithRuleCode_...
    public async Task ExceptionWithRuleCode_ReturnsRuleCodeField(
        string url,
        string expectedRuleCode
    )
    {
        var response = await _client.GetAsync(url);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedRuleCode, problem.GetProperty("ruleCode").GetString());
    }

    [Fact]
    public async Task ValidationException_Returns400WithErrorsPerField()
    {
        var response = await _client.GetAsync("/test/errors/validation");

        var problem = await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        Assert.Equal(2, errors.GetProperty("Email").GetArrayLength());
        Assert.Equal(1, errors.GetProperty("Phone").GetArrayLength());
    }

    [Fact]
    public async Task UnknownRoute_Returns404AsProblemDetails()
    {
        var response = await _client.GetAsync("/this-route-does-not-exist");

        await AssertProblemDetailsAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnexpectedException_InDevelopment_Returns500WithMessageButNoStackTrace()
    {
        var response = await _client.GetAsync("/test/errors/unexpected");

        var problem = await AssertProblemDetailsAsync(response, HttpStatusCode.InternalServerError);
        Assert.Equal("An unexpected error occurred.", problem.GetProperty("title").GetString());
        Assert.Equal("Something broke deep inside.", problem.GetProperty("detail").GetString());

        var rawBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("   at ", rawBody); // stack trace lines look like "   at Namespace.Method()"
    }

    [Fact]
    public async Task UnexpectedException_InProduction_HidesInternalDetails()
    {
        // A second API instance, this time with the Production environment (NFR01).
        await using var productionFactory = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production")
        );
        var client = productionFactory.CreateClient();

        var response = await client.GetAsync("/test/errors/unexpected");

        var problem = await AssertProblemDetailsAsync(response, HttpStatusCode.InternalServerError);
        Assert.False(problem.TryGetProperty("detail", out _)); // null "detail" is not serialized at all
    }

    /// <summary>Common checks for every error response; returns the parsed JSON body.</summary>
    private static async Task<JsonElement> AssertProblemDetailsAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus
    )
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
        return problem;
    }
}
