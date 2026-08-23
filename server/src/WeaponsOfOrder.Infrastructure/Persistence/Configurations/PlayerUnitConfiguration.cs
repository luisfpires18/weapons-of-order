using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class PlayerUnitConfiguration : IEntityTypeConfiguration<PlayerUnit>
{
    public void Configure(EntityTypeBuilder<PlayerUnit> builder)
    {
        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.DefinitionKey).HasMaxLength(64).IsRequired();
        builder.Property(unit => unit.StarterGrantKey).HasMaxLength(64);
        builder.Property(unit => unit.Origin).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder
            .HasOne<WeaponsOfOrderUser>()
            .WithMany()
            .HasForeignKey(unit => unit.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // The whole of the starter grant's idempotency, and deliberately the only uniqueness
        // on this table. Two first loads racing each other both insert; one of them loses this
        // index and re-reads the winner. The filter is what keeps duplicates possible: a Unit
        // acquired any other way records no grant, and excluded rows are not compared at all,
        // so an account can hold as many copies of a Regular Unit as it acquires.
        builder
            .HasIndex(unit => new { unit.OwnerUserId, unit.StarterGrantKey })
            .IsUnique()
            .HasFilter($"\"{nameof(PlayerUnit.StarterGrantKey)}\" IS NOT NULL")
            .HasDatabaseName("IX_PlayerUnits_OwnerUserId_StarterGrantKey");

        // The roster read: one account's Units, oldest first so the order a player sees does
        // not shuffle between loads.
        builder.HasIndex(unit => new { unit.OwnerUserId, unit.AcquiredAt });

        // Principal side of the composite foreign key on EquippedWeapons. It is what makes
        // "this weapon's Unit and this weapon's item belong to the same account" a constraint
        // rather than a check the service is trusted to perform.
        builder.HasAlternateKey(unit => new { unit.Id, unit.OwnerUserId });
    }
}
