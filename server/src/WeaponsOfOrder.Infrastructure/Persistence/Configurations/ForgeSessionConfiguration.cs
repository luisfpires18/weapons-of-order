using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeaponsOfOrder.Infrastructure.Gameplay;
using WeaponsOfOrder.Infrastructure.Identity;

namespace WeaponsOfOrder.Infrastructure.Persistence.Configurations;

internal sealed class ForgeSessionConfiguration : IEntityTypeConfiguration<ForgeSession>
{
    public void Configure(EntityTypeBuilder<ForgeSession> builder)
    {
        builder.HasKey(session => session.Id);

        builder.Property(session => session.RecipeKey).HasMaxLength(64).IsRequired();

        // Stored as text. The rows are read by people during development, and a status that
        // reads as 'Active' rather than 0 is worth more than the bytes it costs. It also
        // means reordering the enum cannot silently reinterpret existing rows.
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(session => session.Craftsmanship).HasConversion<string>().HasMaxLength(16);

        builder
            .HasOne<WeaponsOfOrderUser>()
            .WithMany()
            .HasForeignKey(session => session.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(session => session.Strikes)
            .WithOne(strike => strike.Session!)
            .HasForeignKey(strike => strike.ForgeSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // One workpiece on the anvil at a time. This is the guarantee, not the check in
        // BeginAsync: two begin requests arriving together both pass an application-level
        // check, and only one of them can win a unique index. The loser's whole transaction
        // rolls back, materials included, so a duplicate request cannot be charged for.
        builder
            .HasIndex(session => session.OwnerUserId)
            .IsUnique()
            .HasFilter($"\"{nameof(ForgeSession.Status)}\" = '{nameof(ForgeSessionStatus.Active)}'")
            .HasDatabaseName("IX_ForgeSessions_OwnerUserId_Active");

        // The forge screen asks for the player's most recent operation on every load.
        builder.HasIndex(session => new { session.OwnerUserId, session.StartedAt });
    }
}
