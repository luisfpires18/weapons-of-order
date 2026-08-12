namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// How hot the workpiece is, in the four readable states the blacksmith canon names.
/// </summary>
/// <remarks>
/// The band is what the game reasons about. The underlying temperature is a continuous
/// value the server keeps so it can decide the band at the instant a strike lands, and so
/// the client has something to draw a gauge from.
/// </remarks>
public enum HeatBand
{
    Cold = 0,
    Workable = 1,
    Ideal = 2,
    Burning = 3,
}

/// <summary>
/// Physical craftsmanship quality of an ordinary forged item.
/// </summary>
/// <remarks>
/// Canon is explicit that ordinary blacksmithing has no Legendary tier, and that this is
/// how well the object was made rather than a magical rarity. Rune identity, weapon
/// category and Aura level are separate dimensions and are not stored here.
/// </remarks>
public enum Craftsmanship
{
    Common = 0,
    Rare = 1,
    Epic = 2,
}

/// <summary>The life of a single forge operation.</summary>
public enum ForgeSessionStatus
{
    /// <summary>Materials are paid and the workpiece is on the anvil.</summary>
    Active = 0,

    /// <summary>Every required strike landed and an item was created.</summary>
    Completed = 1,

    /// <summary>Left in the fire past the grace period. No item is created.</summary>
    Ruined = 2,

    /// <summary>Given up by the player. No item, and no material refund.</summary>
    Abandoned = 3,
}

/// <summary>
/// Where an owned item came from, kept so a later system can tell a normally forged weapon
/// apart from one that arrived some other way.
/// </summary>
/// <remarks>
/// Only ordinary forging exists, so only ordinary forging is named. Runeforged, looted or
/// granted origins are added by the systems that actually create them.
/// </remarks>
public enum ForgedItemOrigin
{
    OrdinaryForge = 0,
}
