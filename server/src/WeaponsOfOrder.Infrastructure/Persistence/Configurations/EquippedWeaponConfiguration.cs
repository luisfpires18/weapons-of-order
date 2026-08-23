using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class EquippedWeaponConfiguration : IEntityTypeConfiguration<EquippedWeapon>
{
    public void Configure(EntityTypeBuilder<EquippedWeapon> builder)
    {
        // One physical item, one place. Making the item the primary key is the cheapest
        // possible statement of that: there is no row shape in which a sword is in two hands.
        builder.HasKey(equipped => equipped.ItemId);

        builder.Ignore(equipped => equipped.Slots);

        // A weapon has to be in a hand. Without this a row could exist that equips nothing,
        // which would hold an item hostage while occupying no slot.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_EquippedWeapons_OccupiesASlot",
            $"\"{nameof(EquippedWeapon.OccupiesFirstSlot)}\" OR \"{nameof(EquippedWeapon.OccupiesSecondSlot)}\""));

        // Composite, so the account travels with both ends. A request that somehow named one
        // player's Unit and another player's sword cannot be written down at all.
        builder
            .HasOne<ForgedItem>()
            .WithOne()
            .HasPrincipalKey<ForgedItem>(item => new { item.Id, item.OwnerUserId })
            .HasForeignKey<EquippedWeapon>(equipped => new { equipped.ItemId, equipped.OwnerUserId })
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<PlayerUnit>()
            .WithMany()
            .HasPrincipalKey(unit => new { unit.Id, unit.OwnerUserId })
            .HasForeignKey(equipped => new { equipped.PlayerUnitId, equipped.OwnerUserId })
            .OnDelete(DeleteBehavior.Cascade);

        // Two hands, two indexes. This is the guarantee that a slot holds one weapon, not the
        // service's look-before-you-write: two equip requests for one empty hand both find it
        // free, and only one of them can win the index. The loser's transaction rolls back
        // whole and is reported as a conflict.
        //
        // Both are declared with an explicit model name because they cover the same column and
        // differ only by filter. Without one, the second declaration would be treated as a
        // redefinition of the first and only one of the two hands would be protected.
        builder
            .HasIndex([nameof(EquippedWeapon.PlayerUnitId)], "FirstSlotOccupant")
            .IsUnique()
            .HasFilter($"\"{nameof(EquippedWeapon.OccupiesFirstSlot)}\"")
            .HasDatabaseName("IX_EquippedWeapons_PlayerUnitId_FirstSlot");

        builder
            .HasIndex([nameof(EquippedWeapon.PlayerUnitId)], "SecondSlotOccupant")
            .IsUnique()
            .HasFilter($"\"{nameof(EquippedWeapon.OccupiesSecondSlot)}\"")
            .HasDatabaseName("IX_EquippedWeapons_PlayerUnitId_SecondSlot");

        // The inventory read asks what this account currently has in hand.
        builder.HasIndex(equipped => equipped.OwnerUserId);
    }
}
