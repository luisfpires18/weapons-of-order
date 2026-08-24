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
        // Migration scaffolding only needs a syntactically valid SQLite connection string;
        // it does not connect. Applying migrations still goes through the API configuration.
        var connectionString =
            Environment.GetEnvironmentVariable($"ConnectionStrings__{PersistenceServiceCollectionExtensions.ConnectionStringName}")
            ?? "Data Source=weapons-of-order-design.db";

        var options = new DbContextOptionsBuilder<WeaponsOfOrderDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new WeaponsOfOrderDbContext(options);
    }
}
