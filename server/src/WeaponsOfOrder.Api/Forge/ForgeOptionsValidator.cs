using Microsoft.Extensions.Options;

namespace WeaponsOfOrder.Api.Forge;

/// <summary>
/// Refuses to start when the forge is tuned into a state it cannot run in.
/// </summary>
/// <remarks>
/// These values are meant to be edited, which is exactly why they are checked at startup. A
/// band ordering typed the wrong way round would not throw; it would quietly make Ideal
/// unreachable, and the first person to notice would be a player wondering why every sword
/// is Common.
/// </remarks>
internal sealed class ForgeOptionsValidator : IValidateOptions<ForgeOptions>
{
    public ValidateOptionsResult Validate(string? name, ForgeOptions options)
    {
        var failures = new List<string>();
        var heat = options.Heat;

        if (!(0 < heat.WorkableFrom
            && heat.WorkableFrom < heat.IdealFrom
            && heat.IdealFrom < heat.BurningFrom
            && heat.BurningFrom <= heat.MaxTemperature))
        {
            failures.Add(
                $"'{ForgeOptions.SectionName}:Heat' band boundaries must ascend: 0 < WorkableFrom < "
                + "IdealFrom < BurningFrom <= MaxTemperature.");
        }

        if (heat.HeatRatePerSecond <= 0 || heat.CoolRatePerSecond <= 0)
        {
            failures.Add(
                $"'{ForgeOptions.SectionName}:Heat' HeatRatePerSecond and CoolRatePerSecond must be "
                + "greater than zero, or the workpiece never changes temperature.");
        }

        if (heat.BurnGraceSeconds <= 0)
        {
            failures.Add(
                $"'{ForgeOptions.SectionName}:Heat:BurnGraceSeconds' must be greater than zero. Without "
                + "a grace period a workpiece is ruined the instant it reaches the Burning band, which "
                + "the canon does not allow.");
        }

        if (heat.LossPerStrike < 0)
        {
            failures.Add($"'{ForgeOptions.SectionName}:Heat:LossPerStrike' cannot be negative.");
        }

        if (options.Strike.Required < 1)
        {
            failures.Add($"'{ForgeOptions.SectionName}:Strike:Required' must be at least one strike.");
        }

        if (options.Strike.CooldownSeconds < 0)
        {
            failures.Add($"'{ForgeOptions.SectionName}:Strike:CooldownSeconds' cannot be negative.");
        }

        if (options.Craftsmanship.EpicScore < options.Craftsmanship.RareScore)
        {
            failures.Add(
                $"'{ForgeOptions.SectionName}:Craftsmanship:EpicScore' must be at least RareScore, or "
                + "Rare is unreachable.");
        }

        if (options.Recipes.Count == 0)
        {
            failures.Add($"'{ForgeOptions.SectionName}:Recipes' must contain at least one recipe.");
        }

        foreach (var (key, recipe) in options.Recipes)
        {
            if (string.IsNullOrWhiteSpace(recipe.DisplayName) || string.IsNullOrWhiteSpace(recipe.WeaponType))
            {
                failures.Add(
                    $"Recipe '{key}' needs both a DisplayName and a WeaponType. WeaponType must be a "
                    + "type the weapon registry actually names.");
            }

            if (recipe.Metal < 0 || recipe.Wood < 0 || recipe.Leather < 0)
            {
                failures.Add($"Recipe '{key}' cannot cost a negative amount of any material.");
            }
        }

        var start = options.StartingMaterials;
        if (start.Metal < 0 || start.Wood < 0 || start.Leather < 0)
        {
            failures.Add($"'{ForgeOptions.SectionName}:StartingMaterials' cannot be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
