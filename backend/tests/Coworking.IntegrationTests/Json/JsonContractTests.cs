using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Coworking.IntegrationTests.Fixtures;

namespace Coworking.IntegrationTests.Json;

/// <summary>
/// The JSON "contract" between API and frontend (CW-12/CW-13): numbers are real numbers,
/// enums are names – in both directions. Anything else is a 400 before any code runs.
/// </summary>
public sealed class JsonContractTests(AuthTestingApiFactory factory) : IClassFixture<AuthTestingApiFactory>
{
    [Fact]
    public async Task ValidBody_IsEchoedWithEnumAsName()
    {
        var response = await PostAsync("""{"count":5,"role":"Manager"}""");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, body.GetProperty("count").GetInt32());
        Assert.Equal("Manager", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task NumberWrittenAsString_Returns400()
    {
        var response = await PostAsync("""{"count":"5","role":"Manager"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EnumWrittenAsNumber_Returns400()
    {
        // Without allowIntegerValues: false, 2 would silently be read as Administrator.
        var response = await PostAsync("""{"count":5,"role":2}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEnumName_Returns400()
    {
        var response = await PostAsync("""{"count":5,"role":"Boss"}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostAsync(string json)
    {
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        return await client.PostAsync("/test/json/echo", content);
    }
}
