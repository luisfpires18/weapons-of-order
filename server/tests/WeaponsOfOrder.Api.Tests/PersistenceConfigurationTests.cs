using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Identity;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

public sealed class PersistenceConfigurationTests
{
    [Fact]
    public void Missing_connection_string_fails_fast()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddWeaponsOfOrderPersistence(configuration, AppContext.BaseDirectory));

        Assert.Contains("WeaponsOfOrder", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Configured_connection_string_resolves_a_sqlite_context()
    {
        using var temporary = new TemporaryDatabase();
        using var provider = temporary.BuildProvider();

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", context.Database.ProviderName);
    }

    [Fact]
    public void A_relative_data_source_is_resolved_against_the_content_root()
    {
        // SQLite would otherwise resolve it against whatever directory the process was
        // started from, so `dotnet run` from the repository root and from the project would
        // open two different databases.
        using var temporary = new TemporaryDatabase();

        using var provider = temporary.BuildProvider(
            connectionString: "Data Source=nested/relative.db",
            contentRootPath: temporary.Directory);

        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
        context.Database.OpenConnection();

        Assert.True(File.Exists(Path.Combine(temporary.Directory, "nested", "relative.db")));
    }

    [Fact]
    public void The_directory_holding_the_database_is_created()
    {
        // Staging keeps the file at /home/data/…, outside the deployed application. SQLite
        // creates a missing file but not a missing directory, so without this the first
        // request after a fresh provision fails with "unable to open database file".
        using var temporary = new TemporaryDatabase();
        var directory = Path.Combine(temporary.Directory, "does", "not", "exist", "yet");

        using var provider = temporary.BuildProvider(
            connectionString: $"Data Source={Path.Combine(directory, "weapons-of-order.db")}");

        Assert.True(Directory.Exists(directory));

        // And the provider is usable, not merely constructed.
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>().Database.OpenConnection();
    }
}

/// <summary>
/// The migration path Browser V1 actually uses: a SQLite file that the application brings up
/// to date itself, on a single instance, and that outlives the process.
/// </summary>
public sealed class SqliteMigrationTests
{
    [Fact]
    public async Task A_fresh_database_gets_the_whole_schema()
    {
        using var temporary = new TemporaryDatabase();
        Assert.False(File.Exists(temporary.DatabaseFile));

        await temporary.MigrateAsync();

        using var provider = temporary.BuildProvider();
        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>().Database;

        Assert.Empty(await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.NotEmpty(await database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Migrating_an_up_to_date_database_again_changes_nothing()
    {
        // Every staging start runs this. It has to be a no-op on the second one, and on the
        // hundredth, without touching a row.
        using var temporary = new TemporaryDatabase();
        await temporary.MigrateAsync();

        var userId = await temporary.WriteAnAccountAsync();

        await temporary.MigrateAsync();
        await temporary.MigrateAsync();

        Assert.True(await temporary.AccountExistsAsync(userId));
    }

    [Fact]
    public async Task Startup_migration_is_off_unless_the_environment_asks_for_it()
    {
        // The default has to be off: a real PostgreSQL production environment must not
        // inherit self-migration because Browser V1 found it convenient.
        using var temporary = new TemporaryDatabase();

        using var provider = temporary.BuildProvider();
        await provider.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>().Database;

        Assert.NotEmpty(await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Startup_migration_brings_a_fresh_database_up_and_leaves_an_existing_one_alone()
    {
        using var temporary = new TemporaryDatabase();

        using (var first = temporary.BuildProvider(migrateOnStartup: true))
        {
            await first.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);
        }

        var userId = await temporary.WriteAnAccountAsync();

        // A second start against the same file. Nothing is recreated and nothing is dropped.
        using (var second = temporary.BuildProvider(migrateOnStartup: true))
        {
            await second.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);
        }

        Assert.True(await temporary.AccountExistsAsync(userId));
    }

    [Fact]
    public async Task Write_ahead_logging_is_on_after_startup()
    {
        // One writer and many concurrent readers, rather than a write that blocks every read.
        using var temporary = new TemporaryDatabase();

        using var provider = temporary.BuildProvider(migrateOnStartup: true);
        await provider.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);

        using var scope = provider.CreateScope();
        var mode = await scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>()
            .Database.SqlQueryRaw<string>("PRAGMA journal_mode;")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal("wal", mode.Single(), ignoreCase: true);
    }

    [Fact]
    public async Task Player_data_survives_an_application_restart()
    {
        // The whole point of putting the file on persistent storage. Two providers, built and
        // torn down in turn, are as close to a redeployment as a test gets: the second one
        // shares nothing with the first except the path.
        using var temporary = new TemporaryDatabase();

        Guid userId;
        Guid itemId;

        using (var first = temporary.BuildProvider(migrateOnStartup: true))
        {
            await first.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);

            using var scope = first.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

            var user = TemporaryDatabase.NewAccount();
            userId = user.Id;
            itemId = Guid.NewGuid();

            var session = new ForgeSession
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                RecipeKey = "weapon.sword",
                Status = ForgeSessionStatus.Completed,
                StartedAt = DateTimeOffset.UtcNow,
                TemperatureAt = DateTimeOffset.UtcNow,
            };

            context.Add(user);
            context.Add(session);
            context.Add(new ForgedItem
            {
                Id = itemId,
                OwnerUserId = userId,
                ForgeSessionId = session.Id,
                RecipeKey = "weapon.sword",
                WeaponType = "Sword",
                Craftsmanship = Craftsmanship.Common,
                Origin = ForgedItemOrigin.OrdinaryForge,
                ForgedAt = DateTimeOffset.UtcNow,
            });

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The process is gone. The file is not.
        using (var second = temporary.BuildProvider(migrateOnStartup: true))
        {
            await second.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);

            using var scope = second.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

            Assert.NotNull(await context.Users.FindAsync([userId], TestContext.Current.CancellationToken));

            var item = await context.ForgedItems.FindAsync([itemId], TestContext.Current.CancellationToken);
            Assert.NotNull(item);
            Assert.Equal("Sword", item.WeaponType);
        }
    }
}

/// <summary>
/// A SQLite database in its own temporary directory, removed when the test finishes.
/// </summary>
internal sealed class TemporaryDatabase : IDisposable
{
    public TemporaryDatabase()
    {
        Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"woo-persistence-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(Directory);
        DatabaseFile = System.IO.Path.Combine(Directory, "weapons-of-order.db");
    }

    public string Directory { get; }

    public string DatabaseFile { get; }

    public static WeaponsOfOrderUser NewAccount()
    {
        var email = $"survivor-{Guid.NewGuid():N}@example.test";
        return new WeaponsOfOrderUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        };
    }

    public ServiceProvider BuildProvider(
        string? connectionString = null,
        string? contentRootPath = null,
        bool migrateOnStartup = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WeaponsOfOrder"] = connectionString ?? $"Data Source={DatabaseFile}",
                ["Database:MigrateOnStartup"] = migrateOnStartup ? "true" : "false",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddWeaponsOfOrderPersistence(configuration, contentRootPath ?? Directory)
            .BuildServiceProvider();
    }

    public async Task MigrateAsync()
    {
        using var provider = BuildProvider(migrateOnStartup: true);
        await provider.MigrateWeaponsOfOrderDatabaseAsync(TestContext.Current.CancellationToken);
    }

    public async Task<Guid> WriteAnAccountAsync()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        var user = NewAccount();
        context.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    public async Task<bool> AccountExistsAsync(Guid userId)
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        return await context.Users.AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        // The pool holds the file open until it is emptied, and Windows will not delete it
        // while a handle is alive.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temporary directory is not worth failing a test over.
        }
    }
}
