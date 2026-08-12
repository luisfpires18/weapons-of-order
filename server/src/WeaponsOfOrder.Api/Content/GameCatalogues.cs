using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Content;

/// <summary>A Unit definition, parsed and ready to use.</summary>
/// <remarks>
/// The resolved form of one <c>units.json</c> entry. Everything except <paramref name="Key"/>
/// is content the creator may change at any time; nothing is ever copied onto a player's Unit
/// row.
/// </remarks>
internal sealed record UnitDefinition(
    string Key,
    string DisplayName,
    UnitType Type,
    string Kingdom,
    int Tier,
    ArmorClass MaxArmor,
    bool Mounted,
    bool Starter);

/// <summary>A weapon's structural metadata, parsed and ready to use.</summary>
internal sealed record WeaponDefinition(string Type, string DisplayName, int SlotCost);

/// <summary>
/// The Unit definitions currently authored, keyed for lookup.
/// </summary>
/// <remarks>
/// Built per request from <see cref="IOptionsSnapshot{TOptions}"/>, so an edit to the content
/// file is picked up without a restart and is validated again as it is. The catalogue is three
/// entries; rebuilding it costs nothing worth caching around.
/// </remarks>
internal sealed class UnitCatalogue
{
    private readonly Dictionary<string, UnitDefinition> _byKey;

    public UnitCatalogue(IOptionsSnapshot<UnitContentOptions> options)
    {
        // Validation has already run by the time the snapshot is handed over, so the parses
        // below cannot fail on content that reached here.
        Definitions =
        [
            .. options.Value.Units.Select(unit => new UnitDefinition(
                unit.Key.Trim(),
                unit.DisplayName.Trim(),
                Enum.Parse<UnitType>(unit.Type, ignoreCase: true),
                unit.Kingdom.Trim(),
                unit.Tier,
                Enum.Parse<ArmorClass>(unit.MaxArmor, ignoreCase: true),
                ContentFlag.Read(unit.Mounted),
                ContentFlag.Read(unit.Starter))),
        ];

        _byKey = Definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);
    }

    public IReadOnlyList<UnitDefinition> Definitions { get; }

    /// <summary>The definitions every account is granted one of, in authored order.</summary>
    public IReadOnlyList<UnitDefinition> Starters => [.. Definitions.Where(definition => definition.Starter)];

    public bool TryGet(string key, [NotNullWhen(true)] out UnitDefinition? definition)
        => _byKey.TryGetValue(key, out definition);

    /// <summary>
    /// Where a definition sits in the content file, for ordering a roster.
    /// </summary>
    /// <remarks>
    /// The order the creator wrote them in is the order a player sees them in. Ordering by
    /// anything the rows themselves carry would not do: units granted together share an
    /// acquisition time, and their identifiers are not meaningfully ordered within it.
    /// </remarks>
    public int PositionOf(string key)
    {
        var position = Definitions.ToList().FindIndex(definition => definition.Key == key);

        return position < 0 ? int.MaxValue : position;
    }
}

/// <summary>The weapon types the game can currently put in a Unit's hands.</summary>
internal sealed class WeaponCatalogue
{
    private readonly Dictionary<string, WeaponDefinition> _byType;

    public WeaponCatalogue(IOptionsSnapshot<WeaponContentOptions> options)
        => _byType = options.Value.Weapons
            .Select(weapon => new WeaponDefinition(weapon.Type.Trim(), weapon.DisplayName.Trim(), weapon.SlotCost))
            .ToDictionary(weapon => weapon.Type, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The metadata for a weapon type, or <see langword="null"/> when none is authored.
    /// </summary>
    /// <remarks>
    /// A null answer means the type cannot be equipped yet. It is not a fallback opportunity:
    /// inventing a slot cost for an unauthored weapon would invent canon.
    /// </remarks>
    public WeaponDefinition? Find(string weaponType)
        => _byType.GetValueOrDefault(weaponType);
}
