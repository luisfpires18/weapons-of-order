using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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
/// Gameplay tables still arrive with the tasks that own those systems; inventing them
/// here would bake guesses into the schema before the design authority calls for them.
/// </para>
/// </remarks>
public sealed class WeaponsOfOrderDbContext(DbContextOptions<WeaponsOfOrderDbContext> options)
    : IdentityUserContext<WeaponsOfOrderUser, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WeaponsOfOrderDbContext).Assembly);
    }
}
