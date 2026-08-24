using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Combat;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class ArmyPlacementConfiguration : IEntityTypeConfiguration<ArmyPlacement>
{
    public void Configure(EntityTypeBuilder<ArmyPlacement> builder)
    {
        // One Unit, one place in the army. Making the account and the Unit the primary key is the
        // cheapest possible statement of that: there is no row shape in which a Unit is deployed on
        // two hexes, or deployed and held in reserve at the same time.
        builder.HasKey(placement => new { placement.OwnerUserId, placement.PlayerUnitId });

        builder.Property(placement => placement.Role).HasConversion<string>().HasMaxLength(16).IsRequired();

        // Composite, so the account travels with the Unit. A request that somehow named another
        // player's Unit cannot be written down at all, whatever the service believed.
        builder
            .HasOne<PlayerUnit>()
            .WithMany()
            .HasPrincipalKey(unit => new { unit.Id, unit.OwnerUserId })
            .HasForeignKey(placement => new { placement.PlayerUnitId, placement.OwnerUserId })
            .OnDelete(DeleteBehavior.Cascade);

        var field = Battlefield.Canonical;

        // The two shapes a placement may take, and nothing between them. Without this a row could
        // claim to be active with no hex, or hold a queue position while standing on the
        // battlefield — states the battle builder would have to invent an answer for.
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ArmyPlacements_RoleShape",
                $"""
                 ("Role" = 'Active' AND "HexColumn" IS NOT NULL AND "HexRow" IS NOT NULL AND "ReserveOrder" IS NULL)
                 OR ("Role" = 'Reserve' AND "HexColumn" IS NULL AND "HexRow" IS NULL AND "ReserveOrder" IS NOT NULL)
                 """);

            // The player's own half of the canonical battlefield: the first four columns of seven
            // rows. Checked here as well as in the API because a row outside it would be an army the
            // simulator refuses to field, with no way back except an edit.
            table.HasCheckConstraint(
                "CK_ArmyPlacements_OwnHalf",
                $"""
                 "HexColumn" IS NULL
                 OR ("HexColumn" >= 0 AND "HexColumn" < {field.HalfColumns}
                     AND "HexRow" >= 0 AND "HexRow" < {field.Rows})
                 """);

            table.HasCheckConstraint("CK_ArmyPlacements_ReserveOrder", "\"ReserveOrder\" IS NULL OR \"ReserveOrder\" >= 0");
        });

        // One Unit per hex. This is the guarantee, not the service's look-before-you-write: two
        // requests placing different Units on one empty hex both find it free, and only one of them
        // can win the index. The loser's transaction rolls back whole and is reported as a conflict.
        builder
            .HasIndex(placement => new { placement.OwnerUserId, placement.HexColumn, placement.HexRow })
            .IsUnique()
            .HasFilter($"\"Role\" = '{nameof(ArmyRole.Active)}'")
            .HasDatabaseName("IX_ArmyPlacements_OwnerUserId_Hex");

        // And one Unit per queue position, so reinforcement order is decided before the battle
        // rather than by whichever row the database happened to return first.
        builder
            .HasIndex(placement => new { placement.OwnerUserId, placement.ReserveOrder })
            .IsUnique()
            .HasFilter($"\"Role\" = '{nameof(ArmyRole.Reserve)}'")
            .HasDatabaseName("IX_ArmyPlacements_OwnerUserId_ReserveOrder");
    }
}
