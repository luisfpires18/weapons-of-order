using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class ForgedItemConfiguration : IEntityTypeConfiguration<ForgedItem>
{
    public void Configure(EntityTypeBuilder<ForgedItem> builder)
    {
        builder.HasKey(item => item.Id);

        builder.Property(item => item.RecipeKey).HasMaxLength(64).IsRequired();
        builder.Property(item => item.WeaponType).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Craftsmanship).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(item => item.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder
            .HasOne<WeaponsOfOrderUser>()
            .WithMany()
            .HasForeignKey(item => item.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One completed forge produces one item. A replayed completion request finds this
        // index in the way rather than minting a second sword from the same session.
        builder
            .HasOne<ForgeSession>()
            .WithOne()
            .HasForeignKey<ForgedItem>(item => item.ForgeSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Newest first is how the forge screen lists them.
        builder.HasIndex(item => new { item.OwnerUserId, item.ForgedAt });
    }
}
