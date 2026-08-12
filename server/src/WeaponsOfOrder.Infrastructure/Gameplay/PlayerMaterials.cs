namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// A player's stock of the three generic crafting materials.
/// </summary>
/// <remarks>
/// The blacksmith canon keeps the material vocabulary small and reusable — Metal, Wood,
/// Leather — so this is one row per account rather than a general inventory of resource
/// types. There is no gathering, production or economy yet; where the opening stock comes
/// from is a separate concern, held in <c>ForgeOptions.StartingMaterials</c> and granted
/// lazily the first time a player opens the forge.
/// </remarks>
public sealed class PlayerMaterials
{
    /// <summary>The account this stock belongs to. Also the key: one stock per player.</summary>
    public Guid OwnerUserId { get; set; }

    public int Metal { get; set; }

    public int Wood { get; set; }

    public int Leather { get; set; }

    /// <summary>When the temporary opening stock was granted.</summary>
    public DateTimeOffset GrantedAt { get; set; }
}
