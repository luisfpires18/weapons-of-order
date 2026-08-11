using System.Net;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Uses its own host so the request budget is not already spent by another class, and so
/// the tightened limit here does not throttle anyone else.
/// </summary>
public sealed class RateLimitTests(TightRateLimitApiFactory factory)
    : IClassFixture<TightRateLimitApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Password_reset_requests_are_capped_per_caller()
    {
        using var client = factory.CreateAuthClient();

        for (var attempt = 0; attempt < TightRateLimitApiFactory.SensitivePermitLimit; attempt++)
        {
            var allowed = await client.PostAsync(
                "/api/auth/forgot-password",
                new { email = TestAccounts.NewEmail($"ratelimit-{attempt}") },
                Cancellation);

            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        var rejected = await client.PostAsync(
            "/api/auth/forgot-password",
            new { email = TestAccounts.NewEmail("ratelimit-over") },
            Cancellation);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("rate_limited", await TestAccounts.ReadProblemCodeAsync(rejected, Cancellation));
        Assert.NotNull(rejected.Headers.RetryAfter);
    }
}
