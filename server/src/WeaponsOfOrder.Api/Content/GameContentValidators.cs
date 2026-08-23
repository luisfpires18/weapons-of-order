using Microsoft.Extensions.Options;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Content;

/// <summary>
/// Refuses to start on malformed Unit content, and says exactly what is wrong with it.
/// </summary>
/// <remarks>
/// Content is meant to be edited by hand, which is precisely why none of it is repaired
/// quietly. A duplicate key would silently shadow a definition that persistent rows point at;
/// a mistyped armour class would become a Unit that can wear nothing. Both are cheaper to find
/// at startup than in a battle.
/// </remarks>
internal sealed class UnitContentValidator : IValidateOptions<UnitContentOptions>
{
    /// <summary>Matches the DefinitionKey column width on the player-owned Unit row.</summary>
    public const int MaxKeyLength = 64;

    public const int MaxDisplayNameLength = 64;

    public ValidateOptionsResult Validate(string? name, UnitContentOptions options)
    {
        var failures = new List<string>();
        var section = UnitContentOptions.SectionName;

        var kingdoms = options.Kingdoms
            .Where(kingdom => !string.IsNullOrWhiteSpace(kingdom))
            .ToHashSet(StringComparer.Ordinal);

        if (kingdoms.Count == 0)
        {
            failures.Add(
                $"'{section}:Kingdoms' must name at least one kingdom. It is the list a Unit's "
                + "Kingdom is checked against, so a typo becomes a startup failure rather than a "
                + "kingdom nobody authored.");
        }

        if (options.Units.Count == 0)
        {
            failures.Add($"'{section}:Units' must contain at least one Unit definition.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (definition, index) in options.Units.Select((definition, index) => (definition, index)))
        {
            var where = $"'{section}:Units[{index}]'";
            var key = definition.Key?.Trim() ?? string.Empty;

            if (key.Length == 0)
            {
                failures.Add($"{where} needs a Key. It is the stable identity player-owned Units store.");
            }
            else
            {
                if (key.Length > MaxKeyLength)
                {
                    failures.Add($"{where} Key '{key}' is longer than {MaxKeyLength} characters.");
                }

                if (key.Any(char.IsWhiteSpace))
                {
                    failures.Add($"{where} Key '{key}' cannot contain whitespace.");
                }

                if (!seen.Add(key))
                {
                    failures.Add(
                        $"{where} repeats the Key '{key}'. Keys identify persistent player-owned "
                        + "Units and must be unique.");
                }
            }

            var label = key.Length == 0 ? where : $"Unit '{key}'";

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                failures.Add($"{label} needs a DisplayName.");
            }
            else if (definition.DisplayName.Length > MaxDisplayNameLength)
            {
                failures.Add($"{label} has a DisplayName longer than {MaxDisplayNameLength} characters.");
            }

            if (!Enum.TryParse<UnitType>(definition.Type, ignoreCase: true, out _))
            {
                failures.Add(
                    $"{label} has Type '{definition.Type}'. It must be one of: "
                    + $"{string.Join(", ", Enum.GetNames<UnitType>())}.");
            }

            if (definition.Tier is < 1 or > 3)
            {
                failures.Add(
                    $"{label} has Tier {definition.Tier}. Fixed tiers are 1, 2 or 3; they are "
                    + "classification tiers, not upgrade levels.");
            }

            if (!Enum.TryParse<ArmorClass>(definition.MaxArmor, ignoreCase: true, out _))
            {
                failures.Add(
                    $"{label} has MaxArmor '{definition.MaxArmor}'. It must be one of: "
                    + $"{string.Join(", ", Enum.GetNames<ArmorClass>())}.");
            }

            if (!ContentFlag.IsValid(definition.Mounted, required: true))
            {
                failures.Add(
                    $"{label} has Mounted '{definition.Mounted}'. It must be stated as true or false; "
                    + "it is the one field on a Unit definition that changes how the Unit behaves.");
            }

            if (!ContentFlag.IsValid(definition.Starter, required: false))
            {
                failures.Add($"{label} has Starter '{definition.Starter}'. It must be true, false or absent.");
            }

            if (!string.IsNullOrWhiteSpace(definition.Kingdom) && !kingdoms.Contains(definition.Kingdom))
            {
                failures.Add(
                    $"{label} belongs to kingdom '{definition.Kingdom}', which is not listed in "
                    + $"'{section}:Kingdoms'.");
            }
            else if (string.IsNullOrWhiteSpace(definition.Kingdom))
            {
                failures.Add($"{label} needs a Kingdom.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Refuses to start on malformed weapon content.</summary>
internal sealed class WeaponContentValidator : IValidateOptions<WeaponContentOptions>
{
    public ValidateOptionsResult Validate(string? name, WeaponContentOptions options)
    {
        var failures = new List<string>();
        var section = WeaponContentOptions.SectionName;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (weapon, index) in options.Weapons.Select((weapon, index) => (weapon, index)))
        {
            var where = $"'{section}:Weapons[{index}]'";
            var type = weapon.Type?.Trim() ?? string.Empty;

            if (type.Length == 0)
            {
                failures.Add(
                    $"{where} needs a Type. It must be a weapon type the canonical registry names.");
            }
            else if (!seen.Add(type))
            {
                failures.Add($"{where} repeats the weapon type '{type}'.");
            }

            var label = type.Length == 0 ? where : $"Weapon '{type}'";

            if (string.IsNullOrWhiteSpace(weapon.DisplayName))
            {
                failures.Add($"{label} needs a DisplayName.");
            }

            if (weapon.SlotCost is < 1 or > Loadout.WeaponSlots)
            {
                failures.Add(
                    $"{label} has SlotCost {weapon.SlotCost}. A weapon consumes 1 or "
                    + $"{Loadout.WeaponSlots} of a Unit's {Loadout.WeaponSlots} weapon slots.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
