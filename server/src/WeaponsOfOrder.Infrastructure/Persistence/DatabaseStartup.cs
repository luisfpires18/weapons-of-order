using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Infrastructure.Persistence;

/// <summary>
/// Whether this process brings its own database up to date, bound from the <c>Database</c>
/// configuration section.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Applies any pending EF Core migrations during startup, before the application serves
    /// its first request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Off by default, and deliberately so: this must be something an environment asks for,
    /// not something every deployment inherits by accident.
    /// </para>
    /// <para>
    /// It is on for Browser V1 because the shape of Browser V1 makes it safe — one App
    /// Service instance, no horizontal scale, and one SQLite file on that instance's own
    /// persistent storage. There is no second process to race, and the migration and the
    /// database ship to the same machine at the same moment.
    /// </para>
    /// <para>
    /// <b>A real PostgreSQL production environment must turn this off</b> and go back to an
    /// explicit migration step outside the application. The moment a second instance exists,
    /// two of them starting together both migrate the same database.
    /// </para>
    /// </remarks>
    public bool MigrateOnStartup { get; set; }
}

/// <summary>
/// The one place the application is allowed to change its own schema.
/// </summary>
public static class DatabaseStartup
{
    /// <summary>
    /// Applies pending migrations, if this environment asked for that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Migrations only. Never <c>EnsureCreated</c>, which builds a schema with no migration
    /// history and leaves a database that can never be migrated afterwards; never a drop or
    /// a recreate; and no seeding — an account, a forged item or a Unit exists because a
    /// player made it.
    /// </para>
    /// <para>
    /// A failure here is allowed to escape. The alternative is an application that starts,
    /// answers its liveness check and then fails every query against a schema that is not
    /// there, which is far harder to see than a container that refuses to come up.
    /// </para>
    /// </remarks>
    public static async Task MigrateWeaponsOfOrderDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseStartup));

        if (!options.MigrateOnStartup)
        {
            logger.LogInformation(
                "Database:MigrateOnStartup is off; the schema is expected to be up to date already.");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>().Database;

        var pending = (await database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("The database schema is up to date.");
        }
        else
        {
            // Names only. They are the migration identifiers already in the repository.
            logger.LogInformation(
                "Applying {Count} pending migration(s): {Migrations}.",
                pending.Length,
                string.Join(", ", pending));

            await database.MigrateAsync(cancellationToken);

            logger.LogInformation("The database schema is now up to date.");
        }

        await EnableWriteAheadLoggingAsync(database, logger, cancellationToken);
    }

    /// <summary>
    /// Puts the database in WAL mode.
    /// </summary>
    /// <remarks>
    /// One writer and many concurrent readers, instead of a write that blocks every read.
    /// This is a web application: a battle being saved should not stall the requests drawing
    /// the screen beside it. The setting is recorded in the database file itself, so this is
    /// idempotent and mostly a no-op after the first start.
    /// <para>
    /// Skipped for an in-memory database, which has no journal and answers this with an
    /// error.
    /// </para>
    /// </remarks>
    private static async Task EnableWriteAheadLoggingAsync(
        Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade database,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!database.IsSqlite())
        {
            return;
        }

        var mode = await database.SqlQueryRaw<string>("PRAGMA journal_mode=WAL;")
            .ToListAsync(cancellationToken);

        var applied = mode.FirstOrDefault() ?? "unknown";

        if (!string.Equals(applied, "wal", StringComparison.OrdinalIgnoreCase))
        {
            // An in-memory database answers "memory" and that is correct for it. Anything
            // else is worth knowing about without being worth refusing to start over.
            logger.LogInformation("SQLite journal mode is {JournalMode}.", applied);
        }
    }
}
