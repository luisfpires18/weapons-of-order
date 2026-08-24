namespace WeaponsOfOrder.Api.Content;

/// <summary>
/// Structural weapon metadata, bound from <c>content/weapons.json</c>.
/// </summary>
/// <remarks>
/// Exactly the metadata the weapon registry names: Power, Critical Chance, Weight, Range and
/// Slot Cost. Nothing beyond it — the registry warns against large generic stat packages on
/// weapons, and Defense belongs only to shields, which do not exist yet.
/// <para>
/// A weapon type absent from this file cannot be equipped. That is the intended behaviour: the
/// registry fixes slot cost for only some types and leaves the rest as authored data, so a
/// default would be an invented rule.
/// </para>
/// </remarks>
internal sealed class WeaponContentOptions
{
    public const string SectionName = "WeaponContent";

    public List<WeaponDefinitionSettings> Weapons { get; set; } = [];
}

internal sealed class WeaponDefinitionSettings
{
    /// <summary>Canonical weapon type from the registry, matched against a forged item's type.</summary>
    public string Type { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>How many of a Unit's two weapon slots this weapon consumes: 1 or 2.</summary>
    public int SlotCost { get; set; }

    /// <summary>Power this weapon adds to its wielder. Both hands of a two-item loadout add theirs.</summary>
    public int Power { get; set; }

    /// <summary>Critical Chance this weapon adds, from 0 to 1.</summary>
    public double CriticalChance { get; set; }

    /// <summary>
    /// <c>Light</c>, <c>Medium</c> or <c>Heavy</c>: how the weapon moves the Attack Interval.
    /// </summary>
    /// <remarks>
    /// The registry says weight participates in Attack Interval alongside the Unit's base value
    /// and armour, and leaves the exact modifier per weight as balance data. What each weight is
    /// worth is configured under <c>Combat:WeightIntervalSeconds</c>, not here.
    /// </remarks>
    public string Weight { get; set; } = string.Empty;

    /// <summary>
    /// Attack range in hexes.
    /// </summary>
    /// <remarks>
    /// The registry's v1 defaults are 1 for an ordinary melee weapon and 3 for the Ranged Weapons
    /// family. It is authored per weapon rather than inferred from the type's family, because the
    /// registry allows a later authored weapon to override its family's default.
    /// </remarks>
    public int Range { get; set; }
}
