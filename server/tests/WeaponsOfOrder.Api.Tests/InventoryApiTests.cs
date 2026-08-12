using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// What Task 4 forged, seen from Task 5's inventory.
/// </summary>
/// <remarks>
/// Every item here is made by running the real forge. Nothing is granted, and nothing is
/// inserted: the point of these is that the two systems are actually connected.
/// </remarks>
public sealed class InventoryApiTests(PreparationApiFactory factory) : IClassFixture<PreparationApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_account_that_has_forged_nothing_owns_nothing()
    {
        using var client = await factory.SignedInAsync("inventory-empty", Cancellation);

        Assert.Empty(await client.ReadInventoryAsync(Cancellation));
    }

    [Fact]
    public async Task A_forged_sword_appears_with_the_identity_the_forge_gave_it()
    {
        using var client = await factory.SignedInAsync("inventory-forged", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);

        var item = Assert.Single(await client.ReadInventoryAsync(Cancellation));

        Assert.Equal(itemId, item.Id);
        Assert.Equal("Sword", item.WeaponType);
        Assert.Equal("Sword", item.Name);
        Assert.Equal("ordinaryforge", item.Origin);
        Assert.Equal(1, item.SlotCost);
        Assert.True(item.Equippable);
        Assert.Null(item.EquippedOn);

        // The craftsmanship the forge decided, unchanged by having been listed somewhere else.
        var fromForge = Assert.Single(await client.ReadItemsAsync(Cancellation));
        Assert.Equal(fromForge.Craftsmanship, item.Craftsmanship);
        Assert.Equal(fromForge.ForgedAt, item.ForgedAt);
    }

    [Fact]
    public async Task Forge_provenance_survives_in_the_stored_row()
    {
        using var client = await factory.SignedInAsync("inventory-provenance", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
        var stored = await db.ForgedItems.AsNoTracking().SingleAsync(item => item.Id == itemId, Cancellation);

        Assert.Equal("weapon.sword", stored.RecipeKey);
        Assert.NotEqual(Guid.Empty, stored.ForgeSessionId);
        Assert.Equal((await client.GetSessionAsync(Cancellation)).AccountId, stored.OwnerUserId);
    }

    [Fact]
    public async Task Equipping_changes_where_an_item_is_and_nothing_about_what_it_is()
    {
        using var client = await factory.SignedInAsync("inventory-equipped", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var before = Assert.Single(await client.ReadInventoryAsync(Cancellation));
        await client.EquipAsync(melee.Id, itemId, Cancellation, slot: 2);
        var after = Assert.Single(await client.ReadInventoryAsync(Cancellation));

        Assert.NotNull(after.EquippedOn);
        Assert.Equal(melee.Id, after.EquippedOn.UnitId);
        Assert.Equal("Melee", after.EquippedOn.UnitName);
        Assert.Equal([2], after.EquippedOn.Slots);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Craftsmanship, after.Craftsmanship);
        Assert.Equal(before.WeaponType, after.WeaponType);
        Assert.Equal(before.ForgedAt, after.ForgedAt);
        Assert.Equal(before.Origin, after.Origin);
    }

    [Fact]
    public async Task One_forge_produces_one_inventory_row()
    {
        using var client = await factory.SignedInAsync("inventory-no-duplicates", Cancellation);

        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, first, Cancellation);

        var items = await client.ReadInventoryAsync(Cancellation);

        Assert.Equal(2, items.Count);
        Assert.Equal([first, second], items.Select(item => item.Id).Order());
        Assert.Single(items, item => item.EquippedOn is not null);
    }

    [Fact]
    public async Task An_item_whose_wield_data_is_not_authored_is_still_owned_but_not_equippable()
    {
        using var client = await factory.SignedInAsync("inventory-unauthored", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);

        // A weapon type the content file does not describe. Slot cost is authored data, and
        // the game refuses to guess one rather than inventing a wield rule.
        await RewriteWeaponTypeAsync(itemId, "Chakram");

        var item = Assert.Single(await client.ReadInventoryAsync(Cancellation));

        Assert.Equal("Chakram", item.WeaponType);
        Assert.Equal("Chakram", item.Name);
        Assert.Null(item.SlotCost);
        Assert.False(item.Equippable);

        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var refused = await client.PostEquipAsync(melee.Id, itemId, Cancellation);

        Assert.Equal("item_not_equippable", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
    }

    private async Task RewriteWeaponTypeAsync(Guid itemId, string weaponType)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        var item = await db.ForgedItems.SingleAsync(candidate => candidate.Id == itemId, Cancellation);
        item.WeaponType = weaponType;

        await db.SaveChangesAsync(Cancellation);
    }
}
