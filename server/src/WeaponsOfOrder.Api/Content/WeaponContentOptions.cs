namespace WeaponsOfOrder.Api.Content;

/// <summary>
/// Structural weapon metadata, bound from <c>content/weapons.json</c>.
/// </summary>
/// <remarks>
/// Only what equipping needs. Power, Critical Chance, Weight and Range belong to the weapon
/// registry and to the combat work that will read them; duplicating the registry here would
/// create a second place for weapon facts to disagree.
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
}
