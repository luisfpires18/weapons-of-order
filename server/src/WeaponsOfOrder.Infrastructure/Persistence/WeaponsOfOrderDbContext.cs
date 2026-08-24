using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Infrastructure.Persistence;

/// <summary>
/// Single persistence context for the modular monolith.
/// </summary>
/// <remarks>
/// Task 2 adds the Identity account tables. The base type is
/// <see cref="IdentityUserContext{TUser, TKey}"/> rather than <c>IdentityDbContext</c>
/// because Browser V1 has no roles: an admin/staff authorization model is explicitly
/// deferred, and the role tables would be dead schema until a real admin feature exists.
/// <para>
/// Task 4 adds the first gameplay tables, for the ordinary forge. Later systems bring their
/// own; nothing is added here ahead of the task that owns it.
/// </para>
/// <para>
/// Task 5 adds the preparation tables: the Units a player owns and the weapons they hold.
/// Unit definitions themselves are not here — they are creator-authored content in
/// <c>server/content/units.json</c>, and a player-owned Unit stores only the key of the
/// definition it is an instance of.
/// </para>
/// </remarks>
public sealed class WeaponsOfOrderDbContext(DbContextOptions<WeaponsOfOrderDbContext> options)
    : IdentityUserContext<WeaponsOfOrderUser, Guid>(options)
{
    public DbSet<PlayerMaterials> PlayerMaterials => Set<PlayerMaterials>();

    public DbSet<ForgeSession> ForgeSessions => Set<ForgeSession>();

    public DbSet<ForgeStrike> ForgeStrikes => Set<ForgeStrike>();

    public DbSet<ForgedItem> ForgedItems => Set<ForgedItem>();

    public DbSet<PlayerUnit> PlayerUnits => Set<PlayerUnit>();

    public DbSet<EquippedWeapon> EquippedWeapons => Set<EquippedWeapon>();

    public DbSet<ArmyPlacement> ArmyPlacements => Set<ArmyPlacement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeaponsOfOrderDbContext).Assembly);
    }

    /// <summary>
    /// One rule for every timestamp in the model, rather than a converter remembered on each
    /// property. See <see cref="UtcTicksConverter"/>: SQLite cannot order by a
    /// <c>DateTimeOffset</c>, and the forge and the inventory both do exactly that.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Covers DateTimeOffset? as well.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }
}
