using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Persistence;

namespace WeaponsOfOrder.Infrastructure;

/// <summary>
/// Composition seam between the web host and persistence. The host knows this method; it
/// does not know SQLite, the context options, or the migrations assembly.
/// </summary>
/// <remarks>
/// Browser V1 is a prototype with one player and no traffic, so the store is a SQLite file:
/// no database server to run locally, none to pay for in staging, and the same provider in
/// development, CI and staging rather than three. PostgreSQL remains the intended direction
/// for a real production environment — see docs/architecture/TECH_STACK.md. Nothing above
/// this method knows which provider it is.
/// </remarks>
public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "WeaponsOfOrder";

    /// <param name="contentRootPath">
    /// What a relative <c>Data Source</c> is resolved against. SQLite resolves one against the
    /// process working directory, which is whatever shell started the application; anchoring
    /// it to the content root instead means `dotnet run` from anywhere opens the same file.
    /// </param>
    public static IServiceCollection AddWeaponsOfOrderPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        // Fail at startup rather than silently falling back to a local default: a
        // misconfigured environment must not look healthy until the first query.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set the " +
                $"ConnectionStrings__{ConnectionStringName} environment variable, or use " +
                "appsettings.Development.json / dotnet user-secrets for local development. " +
                "It is a SQLite connection string, for example 'Data Source=.data/weapons-of-order.db'.");
        }

        var resolved = ResolveDataSource(connectionString, contentRootPath);

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));

        services.AddDbContext<WeaponsOfOrderDbContext>(options => options.UseSqlite(
            resolved,
            sqlite => sqlite.MigrationsAssembly(typeof(WeaponsOfOrderDbContext).Assembly.GetName().Name)));

        return services;
    }

    /// <summary>
    /// Makes a relative <c>Data Source</c> absolute and creates the directory it lives in.
    /// </summary>
    /// <remarks>
    /// The directory matters in staging: the file is at <c>/home/data/…</c> on App Service's
    /// persistent share, deliberately outside the deployed application so a redeployment
    /// cannot replace it, and <c>/home/data</c> does not exist until something creates it.
    /// SQLite creates a missing database file but not a missing directory — without this the
    /// first request after a fresh provision fails with "unable to open database file".
    /// </remarks>
    private static string ResolveDataSource(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);

        // An in-memory database has no path and no directory. The test host uses one only
        // where a file would be pointless.
        if (string.IsNullOrWhiteSpace(builder.DataSource)
            || builder.DataSource.Equals(":memory:", StringComparison.Ordinal)
            || builder.Mode == SqliteOpenMode.Memory)
        {
            return connectionString;
        }

        var path = Path.IsPathRooted(builder.DataSource)
            ? builder.DataSource
            : Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        builder.DataSource = path;
        return builder.ConnectionString;
    }

    /// <summary>
    /// Points ASP.NET Core Identity at the application's own context, so the account
    /// tables live in the same database and migration history as everything else.
    /// </summary>
    /// <remarks>
    /// Exists here rather than in the host for the same reason as the method above: the
    /// web layer composes authentication without needing to know EF Core.
    /// </remarks>
    public static IdentityBuilder AddWeaponsOfOrderIdentityStores(this IdentityBuilder builder)
        => builder.AddEntityFrameworkStores<WeaponsOfOrderDbContext>();
}
