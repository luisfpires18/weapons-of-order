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
/// Hosts the real API pipeline in memory against a real SQLite database on disk.
/// </summary>
/// <remarks>
/// A real file, not an in-memory substitute. Accounts are the subject under test, and
/// Identity's normalization, its unique indexes, the partial indexes the forge and the army
/// rely on, and the check constraints are all things only the actual provider enforces.
/// Nothing has to be installed or started first; the file is created under the temporary
/// directory and replaced at the start of every run. Point
/// <c>WOO_TEST_CONNECTION_STRING</c> elsewhere to override it.
/// <para>
/// The environment is Production so the tests run against the hardened cookie
/// configuration, which is why <see cref="CreateAuthClient"/> uses an https base address:
/// a <c>Secure</c> cookie is not stored by a client on a plain-http origin.
/// </para>
/// </remarks>
public class WeaponsOfOrderApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>
    /// One file for the whole test run, shared by every factory in the process, exactly as
    /// the PostgreSQL database it replaced was shared. Named after the process so two runs
    /// on one machine cannot collide.
    /// </summary>
    private static readonly string DatabasePath = Path.Combine(
        Path.GetTempPath(),
        $"woo-api-tests-{Environment.ProcessId}",
        "weapons-of-order.db");

    /// <summary>
    /// Default Timeout is the busy timeout: several test classes run in parallel against one
    /// file, and a writer that arrives during another write should wait rather than fail.
    /// </summary>
    private static readonly string DefaultConnectionString =
        $"Data Source={DatabasePath};Default Timeout=30";

    private static readonly SemaphoreSlim MigrationGate = new(1, 1);

    private static bool _migrated;

    public WeaponsOfOrderApiFactory()
    {
        // WebApplicationFactory's ConfigureAppConfiguration hooks only apply once the host
        // is built, which is after Program's top-level statements have already read
        // builder.Configuration. The connection string is read there, so an environment
        // variable is the seam that lands in time. Settings resolved later through
        // IOptions — everything under "Auth" and "Database" — can use ConfigurationOverrides.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Production);
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__WeaponsOfOrder",
            Environment.GetEnvironmentVariable("WOO_TEST_CONNECTION_STRING") ?? DefaultConnectionString);
    }

    /// <summary>Captures what a real email provider would have delivered.</summary>
    public CapturingNotificationSender Notifications { get; } = new();

    /// <summary>
    /// Rate limits are lifted so an ordinary test class is not throttled by its own setup,
    /// and a client origin is supplied because startup validation requires one.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string?>> BaseConfiguration =>
    [
        new("Auth:RateLimit:Login:PermitLimit", "1000"),
        new("Auth:RateLimit:Registration:PermitLimit", "1000"),
        new("Auth:RateLimit:Sensitive:PermitLimit", "1000"),
        new("Auth:ClientBaseUrl", "https://localhost"),
    ];

    /// <summary>Applied after <see cref="BaseConfiguration"/>, so a subclass can tighten one setting.</summary>
    protected virtual IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration => [];

    public async ValueTask InitializeAsync()
    {
        await MigrationGate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (_migrated)
            {
                return;
            }

            // Every run starts from nothing, so a test can never pass because of a row an
            // earlier run left behind. The whole directory goes: SQLite leaves -wal and -shm
            // beside the file, and a stale write-ahead log would be replayed into the new one.
            var directory = Path.GetDirectoryName(DatabasePath)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            Directory.CreateDirectory(directory);

            using var scope = Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>().Database;
            await database.MigrateAsync(TestContext.Current.CancellationToken);

            // One writer, many concurrent readers. Test classes run in parallel against this
            // one file, and without it a write blocks every read on the connection pool.
            await database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", TestContext.Current.CancellationToken);

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
        // Merged rather than concatenated: AddInMemoryCollection builds a dictionary and
        // rejects a repeated key, which is exactly what a subclass tightening one setting
        // produces.
        var settings = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in BaseConfiguration.Concat(AdditionalConfiguration))
        {
            settings[setting.Key] = setting.Value;
        }

        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(settings));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAccountNotificationSender>();
            services.AddSingleton<IAccountNotificationSender>(Notifications);
        });
    }
}

/// <summary>
/// Configured with no trusted client origin, to prove the application refuses to start
/// rather than falling back to something derived from the request.
/// </summary>
public sealed class MissingClientOriginApiFactory : WeaponsOfOrderApiFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Auth:ClientBaseUrl", string.Empty),
    ];
}

/// <summary>
/// A tighter budget than any test class needs for setup, so the limiter is what the
/// assertions are actually observing.
/// </summary>
public sealed class TightRateLimitApiFactory : WeaponsOfOrderApiFactory
{
    public const int SensitivePermitLimit = 3;

    protected override IEnumerable<KeyValuePair<string, string?>> AdditionalConfiguration =>
    [
        new("Auth:RateLimit:Sensitive:PermitLimit", SensitivePermitLimit.ToString()),
        new("Auth:RateLimit:Sensitive:WindowMinutes", "15"),
    ];
}
