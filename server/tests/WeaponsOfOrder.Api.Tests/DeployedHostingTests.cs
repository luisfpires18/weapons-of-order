using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Runs the pipeline as it is configured behind a trusted reverse proxy, which is what
/// Azure App Service is.
/// </summary>
public sealed class ProxiedApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Hosting:UseForwardedHeaders", "true"),
    ];
}

/// <summary>
/// The forwarded-header behaviour AUTH_SECURITY.md leaves to deployment.
/// </summary>
/// <remarks>
/// <c>Strict-Transport-Security</c> is the observable proof: the HSTS middleware writes it
/// only when <c>Request.IsHttps</c>, so the header's presence is a direct answer to
/// "did this process believe the request was secure". The same corrected scheme and address
/// are what the session cookie and the rate-limit partitions read.
/// </remarks>
public sealed class ForwardedHeaderTests
{
    /// <summary>
    /// Not localhost. <see cref="HstsOptions.ExcludedHosts"/> exempts loopback names by
    /// default, so a test using one would pass for the wrong reason.
    /// </summary>
    private const string ProxiedOrigin = "http://staging.weaponsoforder.example";

    private const string HstsHeader = "Strict-Transport-Security";

    [Fact]
    public async Task The_proxy_scheme_is_honoured_when_the_deployment_declares_a_proxy()
    {
        using var factory = new ProxiedApiFactory();
        using var client = Client(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(HstsHeader));
    }

    [Fact]
    public async Task A_request_that_was_not_forwarded_as_https_is_not_treated_as_secure()
    {
        using var factory = new ProxiedApiFactory();
        using var client = Client(factory);

        var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains(HstsHeader));
    }

    [Fact]
    public async Task Forwarded_headers_are_ignored_when_no_proxy_is_declared()
    {
        // The default posture. A process reachable without a proxy must not let a caller
        // choose its own scheme or its own address, because the address is what every
        // unauthenticated rate-limit budget is partitioned by.
        using var factory = new WeaponsOfOrderApiFactory();
        using var client = Client(factory);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.Headers.Contains(HstsHeader));
    }

    [Fact]
    public void Only_the_entry_the_platform_wrote_is_read()
    {
        using var factory = new ProxiedApiFactory();
        var options = factory.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        // App Service appends the real caller to whatever the client sent, so the rightmost
        // entry is the trustworthy one and a limit of one is what discards the rest.
        Assert.Equal(1, options.ForwardLimit);

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);

        // Host stays out: account links come from Auth:ClientBaseUrl, never from the
        // request, so rewriting it would only widen what a spoofed header reaches.
        Assert.False(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedHost));
    }

    private static HttpClient Client(WeaponsOfOrderApiFactory factory)
        => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri(ProxiedOrigin),
        });
}

/// <summary>
/// The development-only account notification endpoint must not exist anywhere else. It
/// publishes confirmation and reset links, so exposing it in a deployed environment would
/// hand anybody the ability to take over any account.
/// </summary>
public sealed class DevelopmentEndpointExposureTests(WeaponsOfOrderApiFactory factory)
    : IClassFixture<WeaponsOfOrderApiFactory>
{
    [Fact]
    public async Task The_development_notification_endpoint_is_absent_outside_development()
    {
        // The factory hosts the Production environment, which is what a staging deployment
        // is held to as well: everything that is not Development.
        var response = await factory.CreateClient()
            .GetAsync("/api/dev/account-notifications", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
