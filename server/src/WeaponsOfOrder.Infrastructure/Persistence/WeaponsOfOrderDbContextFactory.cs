using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeaponsOfOrder.Infrastructure.Persistence;

/// <summary>
/// Design-time context factory so <c>dotnet ef</c> can add/script migrations against this
/// project without booting the API host. It is never used at runtime.
/// </summary>
internal sealed class WeaponsOfOrderDbContextFactory : IDesignTimeDbContextFactory<WeaponsOfOrderDbContext>
{
    public WeaponsOfOrderDbContext CreateDbContext(string[] args)
    {
        // Migration scaffolding only needs a syntactically valid Npgsql connection string;
        // it does not connect. Applying migrations still goes through the API configuration.
        var connectionString =
            Environment.GetEnvironmentVariable($"ConnectionStrings__{PersistenceServiceCollectionExtensions.ConnectionStringName}")
            ?? "Host=localhost;Port=5433;Database=weapons_of_order;Username=woo_dev;Password=woo_dev";

        var options = new DbContextOptionsBuilder<WeaponsOfOrderDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WeaponsOfOrderDbContext(options);
    }
}
