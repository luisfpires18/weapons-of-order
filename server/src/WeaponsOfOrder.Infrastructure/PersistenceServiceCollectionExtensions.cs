using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Persistence;

namespace WeaponsOfOrder.Infrastructure;

/// <summary>
/// Composition seam between the web host and persistence. The host knows this method;
/// it does not know Npgsql, the context options, or the migrations assembly.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "WeaponsOfOrder";

    public static IServiceCollection AddWeaponsOfOrderPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        // Fail at startup rather than silently falling back to a local default: a
        // misconfigured environment must not look healthy until the first query.
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set the " +
                $"ConnectionStrings__{ConnectionStringName} environment variable, or use " +
                "appsettings.Development.json / dotnet user-secrets for local development.");
        }

        services.AddDbContext<WeaponsOfOrderDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(WeaponsOfOrderDbContext).Assembly.GetName().Name)));

        return services;
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
