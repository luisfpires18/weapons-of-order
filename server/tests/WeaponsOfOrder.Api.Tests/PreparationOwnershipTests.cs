using System.Net;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Two accounts, one server. Ownership comes from the session cookie and from nowhere else.
/// </summary>
/// <remarks>
/// Task 5 is the first work with routes that take an identifier, so these are the tests that
/// matter most: a unit id in a path is a claim, not a credential. A resource belonging to
/// somebody else is answered exactly as one that does not exist, so a guessed identifier
/// cannot be used to learn which guesses were real.
/// </remarks>
public sealed class PreparationOwnershipTests(PreparationApiFactory factory)
    : IClassFixture<PreparationApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_players_units_are_invisible_to_another()
    {
        using var smith = await factory.SignedInAsync("own-units-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-units-stranger", Cancellation);

        var mine = await smith.ReadUnitsAsync(Cancellation);
        var theirs = await stranger.ReadUnitsAsync(Cancellation);

        Assert.Empty(mine.Select(unit => unit.Id).Intersect(theirs.Select(unit => unit.Id)));
    }

    [Fact]
    public async Task One_players_inventory_is_invisible_to_another()
    {
        using var smith = await factory.SignedInAsync("own-items-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-items-stranger", Cancellation);

        await smith.ForgeSwordAsync(factory, Cancellation);

        Assert.Single(await smith.ReadInventoryAsync(Cancellation));
        Assert.Empty(await stranger.ReadInventoryAsync(Cancellation));
    }

    [Fact]
    public async Task An_account_cannot_equip_its_own_weapon_onto_somebody_elses_unit()
    {
        using var smith = await factory.SignedInAsync("own-cross-unit-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-cross-unit-stranger", Cancellation);

        var sword = await smith.ForgeSwordAsync(factory, Cancellation);
        var theirUnit = await stranger.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var refused = await smith.PostEquipAsync(theirUnit.Id, sword, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("unit_not_found", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));

        Assert.Empty((await stranger.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons);
        Assert.Null(Assert.Single(await smith.ReadInventoryAsync(Cancellation)).EquippedOn);
    }

    [Fact]
    public async Task An_account_cannot_equip_somebody_elses_weapon_onto_its_own_unit()
    {
        using var smith = await factory.SignedInAsync("own-cross-item-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-cross-item-stranger", Cancellation);

        var theirSword = await stranger.ForgeSwordAsync(factory, Cancellation);
        var myUnit = await smith.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var refused = await smith.PostEquipAsync(myUnit.Id, theirSword, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal("item_not_found", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));

        Assert.Empty((await smith.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons);
    }

    [Fact]
    public async Task An_account_cannot_unequip_somebody_elses_weapon()
    {
        using var smith = await factory.SignedInAsync("own-unequip-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-unequip-stranger", Cancellation);

        var sword = await smith.ForgeSwordAsync(factory, Cancellation);
        var myUnit = await smith.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        await smith.EquipAsync(myUnit.Id, sword, Cancellation);

        var theirUnit = await stranger.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        // Naming my unit gets them nothing, and naming their own with my sword gets them
        // nothing either.
        foreach (var attempt in new[]
        {
            await stranger.PostUnequipAsync(myUnit.Id, sword, Cancellation),
            await stranger.PostUnequipAsync(theirUnit.Id, sword, Cancellation),
        })
        {
            Assert.Contains(
                await TestAccounts.ReadProblemCodeAsync(attempt, Cancellation),
                (string?[])["unit_not_found", "item_not_equipped"]);
        }

        Assert.Equal(sword, Assert.Single((await smith.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons).ItemId);
    }

    [Fact]
    public async Task A_guessed_identifier_is_answered_exactly_as_one_that_does_not_exist()
    {
        using var smith = await factory.SignedInAsync("own-guess-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-guess-stranger", Cancellation);

        var theirSword = await stranger.ForgeSwordAsync(factory, Cancellation);
        var theirUnit = await stranger.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var mySword = await smith.ForgeSwordAsync(factory, Cancellation);
        var myUnit = await smith.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var real = await smith.PostEquipAsync(theirUnit.Id, mySword, Cancellation);
        var invented = await smith.PostEquipAsync(Guid.CreateVersion7(), mySword, Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, real.StatusCode);
        Assert.Equal(
            await TestAccounts.ReadComparableBodyAsync(real, Cancellation),
            await TestAccounts.ReadComparableBodyAsync(invented, Cancellation));

        var realItem = await smith.PostEquipAsync(myUnit.Id, theirSword, Cancellation);
        var inventedItem = await smith.PostEquipAsync(myUnit.Id, Guid.CreateVersion7(), Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, realItem.StatusCode);
        Assert.Equal(
            await TestAccounts.ReadComparableBodyAsync(realItem, Cancellation),
            await TestAccounts.ReadComparableBodyAsync(inventedItem, Cancellation));
    }

    [Fact]
    public async Task A_weapon_in_one_accounts_hands_is_never_reported_to_another()
    {
        using var smith = await factory.SignedInAsync("own-report-smith", Cancellation);
        using var stranger = await factory.SignedInAsync("own-report-stranger", Cancellation);

        var sword = await smith.ForgeSwordAsync(factory, Cancellation);
        var myUnit = await smith.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        await smith.EquipAsync(myUnit.Id, sword, Cancellation);

        Assert.Empty(await stranger.ReadInventoryAsync(Cancellation));
        Assert.All(await stranger.ReadUnitsAsync(Cancellation), unit => Assert.Empty(unit.Weapons));
    }
}
