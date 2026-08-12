namespace WeaponsOfOrder.Infrastructure.Gameplay;

/// <summary>
/// The two kinds of Unit the canon names.
/// </summary>
/// <remarks>
/// Regular Units may exist in multiple copies; Heroes are unique. Only the distinction is
/// structural — which Units and which Heroes exist is creator-authored content, never
/// something this assembly decides.
/// </remarks>
public enum UnitType
{
    Regular = 0,
    Hero = 1,
}

/// <summary>
/// The armour classes a Unit's maximum armour is drawn from.
/// </summary>
/// <remarks>
/// A Unit may wear its own class or anything below it. No armour items exist yet, so nothing
/// currently reads this beyond publishing a Unit's limit; the ordering is here because it is
/// canon rather than because a feature needs it today.
/// </remarks>
public enum ArmorClass
{
    Light = 0,
    Medium = 1,
    Heavy = 2,
}

/// <summary>
/// Where a player-owned Unit came from.
/// </summary>
/// <remarks>
/// Only the temporary starter grant exists, so only it is named. Recruitment brings its own
/// origin when recruitment is built.
/// </remarks>
public enum PlayerUnitOrigin
{
    StarterGrant = 0,
}

/// <summary>Structural facts about a loadout that are canon rather than balance.</summary>
public static class Loadout
{
    /// <summary>
    /// Every Unit and Hero has exactly two weapon slots, representing the two hands. A weapon
    /// consumes one or two of them as its content authors it.
    /// </summary>
    public const int WeaponSlots = 2;

    public const int FirstSlot = 1;

    public const int SecondSlot = 2;
}
