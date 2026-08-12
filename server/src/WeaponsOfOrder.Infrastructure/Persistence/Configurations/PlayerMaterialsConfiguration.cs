using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class PlayerMaterialsConfiguration : IEntityTypeConfiguration<PlayerMaterials>
{
    public void Configure(EntityTypeBuilder<PlayerMaterials> builder)
    {
        builder.HasKey(materials => materials.OwnerUserId);

        builder
            .HasOne<WeaponsOfOrderUser>()
            .WithOne()
            .HasForeignKey<PlayerMaterials>(materials => materials.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The last line of defence against a stale read spending materials twice. The forge
        // checks the cost before it charges, but a check in application code is a check that
        // two concurrent requests can both pass; this one they cannot.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_PlayerMaterials_NonNegative",
            $"\"{nameof(PlayerMaterials.Metal)}\" >= 0"
            + $" AND \"{nameof(PlayerMaterials.Wood)}\" >= 0"
            + $" AND \"{nameof(PlayerMaterials.Leather)}\" >= 0"));
    }
}
