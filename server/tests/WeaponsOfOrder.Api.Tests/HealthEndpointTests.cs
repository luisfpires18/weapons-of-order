using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class HealthEndpointTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_reports_healthy_without_touching_the_database()
    {
        var response = await _client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.Equal("weapons-of-order-api", body.GetProperty("service").GetString());
        Assert.Equal("Healthy", body.GetProperty("status").GetString());
        Assert.Empty(body.GetProperty("checks").EnumerateObject());
    }

    [Fact]
    public async Task Readiness_includes_the_database_check()
    {
        var response = await _client.GetAsync("/api/health/ready", TestContext.Current.CancellationToken);

        // Status depends on whether PostgreSQL is reachable from the test host, so the
        // assertion is about the contract, not the verdict.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(
            TestContext.Current.CancellationToken);

        Assert.True(body.GetProperty("checks").TryGetProperty("database", out _));
    }

    [Fact]
    public async Task Unmatched_api_routes_return_404_instead_of_the_spa_document()
    {
        var response = await _client.GetAsync("/api/not-a-real-endpoint", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
