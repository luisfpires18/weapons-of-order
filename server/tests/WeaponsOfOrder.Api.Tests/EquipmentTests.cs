using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Putting a forged weapon into a unit's hands, and taking it out again.
/// </summary>
public sealed class EquipmentTests(PreparationApiFactory factory) : IClassFixture<PreparationApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_forged_sword_can_be_equipped_to_melee()
    {
        using var client = await factory.SignedInAsync("equip-melee", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var updated = await client.EquipAsync(melee.Id, itemId, Cancellation);

        var weapon = Assert.Single(updated.Weapons);
        Assert.Equal(itemId, weapon.ItemId);
        Assert.Equal("Sword", weapon.WeaponType);
        Assert.Equal([1], weapon.Slots);
    }

    [Theory]
    [InlineData(PreparationApi.MeleeKey)]
    [InlineData(PreparationApi.RangedKey)]
    [InlineData(PreparationApi.MountedKey)]
    public async Task A_units_name_restricts_nothing_about_what_it_may_hold(string definitionKey)
    {
        using var client = await factory.SignedInAsync($"equip-any-{definitionKey}", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var unit = await client.UnitAsync(definitionKey, Cancellation);

        var updated = await client.EquipAsync(unit.Id, itemId, Cancellation);

        Assert.Equal(itemId, Assert.Single(updated.Weapons).ItemId);
    }

    [Fact]
    public async Task One_sword_moves_between_units_by_being_unequipped_first()
    {
        using var client = await factory.SignedInAsync("equip-move", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);

        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var ranged = await client.UnitAsync(PreparationApi.RangedKey, Cancellation);
        var mounted = await client.UnitAsync(PreparationApi.MountedKey, Cancellation);

        await client.EquipAsync(melee.Id, itemId, Cancellation);

        // While it is in one unit's hands another cannot take it. There is only one of it.
        var refused = await client.PostEquipAsync(ranged.Id, itemId, Cancellation);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("item_already_equipped", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));

        Assert.Empty((await client.UnequipAsync(melee.Id, itemId, Cancellation)).Weapons);
        Assert.Equal(itemId, Assert.Single((await client.EquipAsync(ranged.Id, itemId, Cancellation)).Weapons).ItemId);

        await client.UnequipAsync(ranged.Id, itemId, Cancellation);
        Assert.Equal(itemId, Assert.Single((await client.EquipAsync(mounted.Id, itemId, Cancellation)).Weapons).ItemId);

        // And exactly one unit is holding it at the end of all that.
        var roster = await client.ReadUnitsAsync(Cancellation);
        Assert.Single(roster, unit => unit.Weapons.Any(weapon => weapon.ItemId == itemId));
    }

    [Fact]
    public async Task Two_distinct_swords_fill_both_hands()
    {
        using var client = await factory.SignedInAsync("equip-both", Cancellation);
        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, first, Cancellation);
        var updated = await client.EquipAsync(melee.Id, second, Cancellation);

        Assert.Equal(2, updated.Weapons.Count);
        Assert.Equal([1], updated.SlotsOf(first));
        Assert.Equal([2], updated.SlotsOf(second));
    }

    [Fact]
    public async Task An_unnamed_slot_takes_the_first_free_hand()
    {
        using var client = await factory.SignedInAsync("equip-default-slot", Cancellation);
        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        // Second hand first, on purpose: the next weapon should then find the first hand free.
        await client.EquipAsync(melee.Id, first, Cancellation, slot: 2);
        var updated = await client.EquipAsync(melee.Id, second, Cancellation);

        Assert.Equal([2], updated.SlotsOf(first));
        Assert.Equal([1], updated.SlotsOf(second));
    }

    [Fact]
    public async Task One_sword_cannot_fill_both_hands()
    {
        using var client = await factory.SignedInAsync("equip-not-twice", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, itemId, Cancellation, slot: 1);
        var refused = await client.PostEquipAsync(melee.Id, itemId, Cancellation, slot: 2);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("item_already_equipped", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));

        var unit = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        Assert.Equal([1], Assert.Single(unit.Weapons).Slots);
    }

    [Fact]
    public async Task An_occupied_hand_is_refused_rather_than_quietly_swapped()
    {
        using var client = await factory.SignedInAsync("equip-occupied", Cancellation);
        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, first, Cancellation, slot: 1);
        var refused = await client.PostEquipAsync(melee.Id, second, Cancellation, slot: 1);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("unit_slot_occupied", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));

        var unit = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        Assert.Equal(first, Assert.Single(unit.Weapons).ItemId);
    }

    [Fact]
    public async Task A_third_weapon_cannot_be_added_to_a_full_loadout()
    {
        using var client = await factory.SignedInAsync("equip-capacity", Cancellation);
        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var third = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, first, Cancellation);
        await client.EquipAsync(melee.Id, second, Cancellation);

        var refused = await client.PostEquipAsync(melee.Id, third, Cancellation);

        Assert.Equal("unit_slot_occupied", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
        Assert.Equal(2, (await client.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons.Count);
    }

    [Fact]
    public async Task A_slot_that_does_not_exist_is_refused()
    {
        using var client = await factory.SignedInAsync("equip-bad-slot", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var refused = await client.PostEquipAsync(melee.Id, itemId, Cancellation, slot: 3);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal("unknown_slot", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
    }

    [Fact]
    public async Task Unequipping_frees_the_weapon_and_the_hand()
    {
        using var client = await factory.SignedInAsync("unequip-frees", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, itemId, Cancellation, slot: 1);
        Assert.Empty((await client.UnequipAsync(melee.Id, itemId, Cancellation)).Weapons);

        Assert.Null(Assert.Single(await client.ReadInventoryAsync(Cancellation)).EquippedOn);

        // The hand is genuinely free again, not merely reported as such.
        Assert.Equal([1], (await client.EquipAsync(melee.Id, itemId, Cancellation, slot: 1)).SlotsOf(itemId));
    }

    [Fact]
    public async Task Unequipping_something_the_unit_is_not_holding_is_refused()
    {
        using var client = await factory.SignedInAsync("unequip-absent", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var ranged = await client.UnitAsync(PreparationApi.RangedKey, Cancellation);

        await client.EquipAsync(melee.Id, itemId, Cancellation);

        var refused = await client.PostUnequipAsync(ranged.Id, itemId, Cancellation);

        Assert.Equal("item_not_equipped", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
        Assert.Equal(itemId, Assert.Single((await client.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons).ItemId);
    }

    [Fact]
    public async Task A_loadout_survives_a_new_session()
    {
        var email = TestAccounts.NewEmail("equip-persists");

        using var first = factory.CreateAuthClient();
        await first.SignInAsNewAccountAsync(factory, email, TestAccounts.ValidPassword, Cancellation);

        var itemId = await first.ForgeSwordAsync(factory, Cancellation);
        var mounted = await first.UnitAsync(PreparationApi.MountedKey, Cancellation);
        await first.EquipAsync(mounted.Id, itemId, Cancellation, slot: 2);

        // A different browser holding the same account.
        using var second = factory.CreateAuthClient();
        await second.SignInAsync(email, TestAccounts.ValidPassword, Cancellation);

        var unit = await second.UnitAsync(PreparationApi.MountedKey, Cancellation);

        Assert.Equal(mounted.Id, unit.Id);
        Assert.True(unit.Mounted);
        Assert.Equal([2], unit.SlotsOf(itemId));
    }

    [Fact]
    public async Task Equipping_requires_an_antiforgery_token()
    {
        using var client = await factory.SignedInAsync("equip-antiforgery", Cancellation);
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        foreach (var path in new[] { "equip", "unequip" })
        {
            var response = await client.PostAsync(
                $"/api/units/{melee.Id}/{path}",
                new { itemId },
                csrfToken: null,
                Cancellation);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("antiforgery", await TestAccounts.ReadProblemCodeAsync(response, Cancellation));
        }

        Assert.Empty((await client.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons);
    }

    [Fact]
    public async Task The_preparation_api_is_closed_to_anonymous_callers()
    {
        using var anonymous = factory.CreateAuthClient();

        foreach (var path in new[] { "/api/units", "/api/inventory/items" })
        {
            var response = await anonymous.Http.GetAsync(path, Cancellation);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var equip = await anonymous.PostAsync(
            $"/api/units/{Guid.CreateVersion7()}/equip",
            new { itemId = Guid.CreateVersion7() },
            Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, equip.StatusCode);
    }
}

/// <summary>
/// A weapon that takes both hands.
/// </summary>
/// <remarks>
/// Bows are canonically two-slot but no bow content exists, so this uses a synthetic type
/// declared only inside the test factory. What it proves is structural: one item, one
/// inventory row, one equipment row, both slots occupied — the shape a real 2-slot weapon will
/// need without the item being duplicated to fill two hands.
/// </remarks>
public sealed class TwoSlotWeaponTests(TwoSlotWeaponApiFactory factory) : IClassFixture<TwoSlotWeaponApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_two_slot_weapon_takes_the_whole_loadout_as_one_item()
    {
        using var client = await factory.SignedInAsync("two-slot", Cancellation);
        var itemId = await ForgeTwoSlotWeaponAsync(client);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var updated = await client.EquipAsync(melee.Id, itemId, Cancellation);

        // One weapon in the loadout, occupying both slots. Not two entries for one object.
        var weapon = Assert.Single(updated.Weapons);
        Assert.Equal([1, 2], weapon.Slots);

        // One inventory row, and one equipment row.
        var item = Assert.Single(await client.ReadInventoryAsync(Cancellation));
        Assert.Equal(2, item.SlotCost);
        Assert.Equal([1, 2], item.EquippedOn!.Slots);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();
        var stored = Assert.Single(
            await db.EquippedWeapons.AsNoTracking()
                .Where(equipped => equipped.PlayerUnitId == melee.Id)
                .ToListAsync(Cancellation));

        Assert.True(stored.OccupiesFirstSlot);
        Assert.True(stored.OccupiesSecondSlot);
    }

    [Fact]
    public async Task A_two_slot_weapon_cannot_be_assigned_to_one_hand()
    {
        using var client = await factory.SignedInAsync("two-slot-hand", Cancellation);
        var itemId = await ForgeTwoSlotWeaponAsync(client);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var refused = await client.PostEquipAsync(melee.Id, itemId, Cancellation, slot: 1);

        Assert.Equal("weapon_needs_both_hands", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
    }

    [Fact]
    public async Task A_two_slot_weapon_is_refused_when_either_hand_is_full()
    {
        using var client = await factory.SignedInAsync("two-slot-blocked", Cancellation);
        var sword = await client.ForgeSwordAsync(factory, Cancellation);
        var twoSlot = await ForgeTwoSlotWeaponAsync(client);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, sword, Cancellation, slot: 2);
        var refused = await client.PostEquipAsync(melee.Id, twoSlot, Cancellation);

        Assert.Equal("unit_slot_occupied", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
    }

    [Fact]
    public async Task Nothing_fits_beside_a_two_slot_weapon()
    {
        using var client = await factory.SignedInAsync("two-slot-exclusive", Cancellation);
        var twoSlot = await ForgeTwoSlotWeaponAsync(client);
        var sword = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        await client.EquipAsync(melee.Id, twoSlot, Cancellation);

        foreach (var slot in new int?[] { null, 1, 2 })
        {
            var refused = await client.PostEquipAsync(melee.Id, sword, Cancellation, slot);
            Assert.Equal("unit_slot_occupied", await TestAccounts.ReadProblemCodeAsync(refused, Cancellation));
        }
    }

    /// <summary>
    /// Forges a real sword and rewrites the weapon type it recorded.
    /// </summary>
    /// <remarks>
    /// The item still has to be a genuine forged item — it keeps its own identity, owner and
    /// forge session — because the loadout is what is under test, not a way around Task 4.
    /// </remarks>
    private async Task<Guid> ForgeTwoSlotWeaponAsync(AuthTestClient client)
    {
        var itemId = await client.ForgeSwordAsync(factory, Cancellation);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        var item = await db.ForgedItems.SingleAsync(candidate => candidate.Id == itemId, Cancellation);
        item.WeaponType = TwoSlotWeaponApiFactory.WeaponType;
        await db.SaveChangesAsync(Cancellation);

        return itemId;
    }
}
