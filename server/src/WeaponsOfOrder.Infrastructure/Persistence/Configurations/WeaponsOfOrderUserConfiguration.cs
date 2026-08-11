using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class WeaponsOfOrderUserConfiguration : IEntityTypeConfiguration<WeaponsOfOrderUser>
{
    public void Configure(EntityTypeBuilder<WeaponsOfOrderUser> builder)
    {
        // Identity enforces unique emails in application code only, which two concurrent
        // registrations can slip past. The database is the one place that cannot race.
        builder.HasIndex(user => user.NormalizedEmail).IsUnique();
    }
}
