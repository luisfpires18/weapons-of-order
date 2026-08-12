using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WeaponsOfOrder.Api.Content;
using WeaponsOfOrder.Infrastructure.Gameplay;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The creator's Unit content, as the application actually reads it.
/// </summary>
/// <remarks>
/// These assert against the real <c>server/content/units.json</c> rather than a fixture, so a
/// content edit that breaks the shape the application depends on is caught here rather than in
/// a browser.
/// </remarks>
public sealed class UnitContentTests(PreparationApiFactory factory) : IClassFixture<PreparationApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_three_configured_starter_definitions_load()
    {
        using var client = await factory.SignedInAsync("content-load", Cancellation);

        var units = await client.ReadUnitsAsync(Cancellation);

        // In the order the content file lists them. Rows granted together share an acquisition
        // time, so the order a player sees is the creator's rather than the database's.
        Assert.Equal(
            [PreparationApi.MeleeKey, PreparationApi.RangedKey, PreparationApi.MountedKey],
            units.Select(unit => unit.DefinitionKey));

        Assert.Equal(["Melee", "Ranged", "Mounted"], units.Select(unit => unit.Name));
    }

    [Fact]
    public async Task Every_configured_definition_carries_its_structural_fields()
    {
        using var client = await factory.SignedInAsync("content-fields", Cancellation);

        foreach (var unit in await client.ReadUnitsAsync(Cancellation))
        {
            Assert.Equal("regular", unit.Type);
            Assert.Equal("Arkazia", unit.Kingdom);
            Assert.Equal(1, unit.Tier);
            Assert.Equal("heavy", unit.MaxArmor);

            // Canon, not configuration: every unit has exactly two weapon slots.
            Assert.Equal(Loadout.WeaponSlots, unit.WeaponSlots);
        }
    }

    [Fact]
    public async Task Mounted_resolves_from_config_and_only_for_the_definition_that_sets_it()
    {
        using var client = await factory.SignedInAsync("content-mounted", Cancellation);

        var units = (await client.ReadUnitsAsync(Cancellation)).ToDictionary(unit => unit.DefinitionKey);

        Assert.True(units[PreparationApi.MountedKey].Mounted);
        Assert.False(units[PreparationApi.MeleeKey].Mounted);
        Assert.False(units[PreparationApi.RangedKey].Mounted);
    }

    [Fact]
    public void Keys_are_unique()
    {
        var options = factory.Services.GetRequiredService<IOptions<UnitContentOptions>>().Value;

        Assert.Equal(
            options.Units.Count,
            options.Units.Select(unit => unit.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Content_carries_no_class_specialisation_or_combat_stat_fields()
    {
        // The definition shape is the guarantee. Canon derives the current class from
        // unit + loadout and the creator has not authored the mappings, so there is nowhere
        // for a placeholder class name to be stored even by accident.
        var properties = typeof(UnitDefinitionSettings).GetProperties().Select(property => property.Name);

        Assert.Equal(
            ["Key", "DisplayName", "Type", "Kingdom", "Tier", "MaxArmor", "Mounted", "Starter"],
            properties);
    }
}

/// <summary>
/// Malformed content stops the application at startup rather than being repaired quietly.
/// </summary>
/// <remarks>
/// Each of these is a mistake a person can make in a text file. None of them is a state worth
/// starting in: a duplicate key shadows a definition that persistent rows point at, and an
/// unreadable armour class or tier would become a unit the rest of the game cannot reason
/// about.
/// </remarks>
public sealed class MalformedUnitContentTests
{
    [Fact]
    public void A_repeated_key_is_rejected()
        => AssertRefusesToStart<DuplicateUnitKeyApiFactory>("arkazia.melee");

    [Fact]
    public void An_empty_display_name_is_rejected()
        => AssertRefusesToStart<UnnamedUnitApiFactory>("DisplayName");

    [Fact]
    public void An_unknown_unit_type_is_rejected()
        => AssertRefusesToStart<UnknownUnitTypeApiFactory>("Champion");

    [Fact]
    public void A_tier_outside_one_to_three_is_rejected()
        => AssertRefusesToStart<OutOfRangeTierApiFactory>("Tier 4");

    [Fact]
    public void An_unknown_armour_class_is_rejected()
        => AssertRefusesToStart<UnknownArmorClassApiFactory>("Plate");

    [Fact]
    public void A_kingdom_that_is_not_listed_is_rejected()
        => AssertRefusesToStart<UnknownKingdomApiFactory>("Somewhere");

    [Fact]
    public void A_slot_cost_outside_the_two_slots_a_unit_has_is_rejected()
        => AssertRefusesToStart<ImpossibleSlotCostApiFactory>("SlotCost 3");

    [Fact]
    public void A_mounted_value_that_is_not_a_boolean_is_rejected()
    {
        // Not left to the configuration binder, which does not fail on a value it cannot
        // convert: it leaves the property at its default, which would quietly put a mounted
        // unit on foot.
        AssertRefusesToStart<UnreadableMountedApiFactory>("Mounted 'sometimes'");
    }

    private static void AssertRefusesToStart<TFactory>(string expectedInMessage)
        where TFactory : WeaponsOfOrderApiFactory, new()
    {
        using var factory = new TFactory();

        var exception = Assert.Throws<OptionsValidationException>(() => _ = factory.Services);

        Assert.Contains(expectedInMessage, exception.Message, StringComparison.Ordinal);
    }
}
