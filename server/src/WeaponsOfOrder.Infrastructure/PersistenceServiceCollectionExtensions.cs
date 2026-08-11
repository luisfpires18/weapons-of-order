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
}
