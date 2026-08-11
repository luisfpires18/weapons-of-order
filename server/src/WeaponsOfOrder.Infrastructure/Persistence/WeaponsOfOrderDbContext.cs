using Microsoft.EntityFrameworkCore;

namespace WeaponsOfOrder.Infrastructure.Persistence;

/// <summary>
/// Single persistence context for the modular monolith.
/// </summary>
/// <remarks>
/// Task 1 deliberately declares no entities. Identity tables arrive with Task 2 and
/// gameplay tables with the tasks that own those systems; inventing them here would
/// bake guesses into the schema before the design authority calls for them.
/// </remarks>
public sealed class WeaponsOfOrderDbContext(DbContextOptions<WeaponsOfOrderDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeaponsOfOrderDbContext).Assembly);
    }
}
