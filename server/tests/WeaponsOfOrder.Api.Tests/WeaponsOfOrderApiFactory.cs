using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using WeaponsOfOrder.Api.Auth.Notifications;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Hosts the real API pipeline in memory against a real PostgreSQL database.
/// </summary>
/// <remarks>
/// Accounts are the subject under test, so the store has to be the actual provider:
/// Identity's normalization, unique indexes and concurrency stamps are not exercised by a
/// substitute. Start the database with <c>docker compose up -d</c>, or point
/// <c>WOO_TEST_CONNECTION_STRING</c> somewhere else.
/// <para>
/// The environment is Production so the tests run against the hardened cookie
/// configuration, which is why <see cref="CreateAuthClient"/> uses an https base address:
/// a <c>Secure</c> cookie is not stored by a client on a plain-http origin.
/// </para>
/// </remarks>
public class WeaponsOfOrderApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=weapons_of_order_tests;Username=woo_dev;Password=woo_dev";

    private static readonly SemaphoreSlim MigrationGate = new(1, 1);

    private static bool _migrated;

    public WeaponsOfOrderApiFactory()
    {
        // WebApplicationFactory's ConfigureAppConfiguration hooks only apply once the host
        // is built, which is after Program's top-level statements have already read
        // builder.Configuration. The connection string is read there, so an environment
        // variable is the seam that lands in time. Settings resolved later through
        // IOptions — everything under "Auth" — can use ConfigurationOverrides instead.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__WeaponsOfOrder",
            Environment.GetEnvironmentVariable("WOO_TEST_CONNECTION_STRING") ?? DefaultConnectionString);
    }

    /// <summary>Captures what a real email provider would have delivered.</summary>
    public CapturingNotificationSender Notifications { get; } = new();

    /// <summary>
    /// Rate limits are lifted by default so an ordinary test class is not throttled by its
    /// own setup. <see cref="RateLimitTests"/> overrides this to assert the real behaviour.
    /// </summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> ConfigurationOverrides =>
    [
        new("Auth:RateLimit:Login:PermitLimit", "1000"),
        new("Auth:RateLimit:Registration:PermitLimit", "1000"),
        new("Auth:RateLimit:Sensitive:PermitLimit", "1000"),
        new("Auth:ClientBaseUrl", "https://localhost"),
    ];

    public async ValueTask InitializeAsync()
    {
        await MigrationGate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (_migrated)
            {
                return;
            }

            using var scope = Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>().Database;
            await database.MigrateAsync(TestContext.Current.CancellationToken);
            _migrated = true;
        }
        finally
        {
            MigrationGate.Release();
        }
    }

    /// <summary>A client with its own cookie jar, as one browser would have.</summary>
    public AuthTestClient CreateAuthClient() => new(CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
    }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(ConfigurationOverrides));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountNotificationSender>();
            services.AddSingleton<IAccountNotificationSender>(Notifications);
        });
    }
}

/// <summary>
/// A tighter budget than any test class needs for setup, so the limiter is what the
/// assertions are actually observing.
/// </summary>
public sealed class TightRateLimitApiFactory : WeaponsOfOrderApiFactory
{
    public const int SensitivePermitLimit = 3;

    protected override IEnumerable<KeyValuePair<string, string?>> ConfigurationOverrides =>
    [
        new("Auth:RateLimit:Sensitive:PermitLimit", SensitivePermitLimit.ToString()),
        new("Auth:RateLimit:Sensitive:WindowMinutes", "15"),
    ];
}
