namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// A persisted item a player owns, created by a completed forge operation.
/// </summary>
/// <remarks>
/// Deliberately thin. It records identity, ownership, what was made and how well — enough
/// for the inventory and equipment work to attach to, and not so much that this task
/// guesses at combat metadata the weapon registry has not authored yet.
/// <para>
/// <see cref="WeaponType"/> is the canonical type from the weapon registry and
/// <see cref="RecipeKey"/> is the content key that produced it. Neither is display copy:
/// the interface may call the item whatever reads best without the stored identity moving.
/// </para>
/// </remarks>
public sealed class ForgedItem
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    /// <summary>The operation that made it. Unique: one completed forge, one item.</summary>
    public Guid ForgeSessionId { get; set; }

    public string RecipeKey { get; set; } = string.Empty;

    /// <summary>Canonical weapon type from the weapon registry, for example <c>Sword</c>.</summary>
    public string WeaponType { get; set; } = string.Empty;

    public Craftsmanship Craftsmanship { get; set; }

    public ForgedItemOrigin Origin { get; set; }

    public DateTimeOffset ForgedAt { get; set; }
}
