using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class ForgeStrikeConfiguration : IEntityTypeConfiguration<ForgeStrike>
{
    public void Configure(EntityTypeBuilder<ForgeStrike> builder)
    {
        builder.HasKey(strike => strike.Id);

        builder.Property(strike => strike.Band).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Two requests from one press both compute the same next ordinal. Only one of them
        // gets to insert it.
        builder.HasIndex(strike => new { strike.ForgeSessionId, strike.Ordinal }).IsUnique();
    }
}
