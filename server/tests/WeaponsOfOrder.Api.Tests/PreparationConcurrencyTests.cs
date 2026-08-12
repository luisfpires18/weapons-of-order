using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WeaponsOfOrder.Infrastructure.Persistence;
using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// Two requests arriving at once.
/// </summary>
/// <remarks>
/// The service checks whether a hand is free before it writes, and that check is not what
/// these tests are about — two requests can both pass it. What settles them is the database:
/// the equipment row's primary key is the item, and each hand has its own filtered unique
/// index. The loser's whole transaction rolls back.
/// <para>
/// None of these sleeps. They issue both requests and wait for both answers.
/// </para>
/// </remarks>
public sealed class PreparationConcurrencyTests(PreparationApiFactory factory)
    : IClassFixture<PreparationApiFactory>
{
    private CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task One_sword_sent_to_two_units_at_once_lands_on_exactly_one()
    {
        using var client = await factory.SignedInAsync("race-one-item", Cancellation);
        var sword = await client.ForgeSwordAsync(factory, Cancellation);

        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var ranged = await client.UnitAsync(PreparationApi.RangedKey, Cancellation);

        var answers = await Task.WhenAll(
            client.PostEquipAsync(melee.Id, sword, Cancellation),
            client.PostEquipAsync(ranged.Id, sword, Cancellation));

        Assert.Single(answers, answer => answer.IsSuccessStatusCode);

        var roster = await client.ReadUnitsAsync(Cancellation);
        Assert.Single(roster, unit => unit.Weapons.Any(weapon => weapon.ItemId == sword));

        // And exactly one row exists for it, whichever request won.
        Assert.Equal(1, await CountEquipmentAsync(equipped => equipped.ItemId == sword));
    }

    [Fact]
    public async Task Two_swords_sent_to_one_hand_at_once_fill_it_once()
    {
        using var client = await factory.SignedInAsync("race-one-slot", Cancellation);
        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var answers = await Task.WhenAll(
            client.PostEquipAsync(melee.Id, first, Cancellation, slot: 1),
            client.PostEquipAsync(melee.Id, second, Cancellation, slot: 1));

        Assert.Single(answers, answer => answer.IsSuccessStatusCode);

        var unit = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var weapon = Assert.Single(unit.Weapons);
        Assert.Equal([1], weapon.Slots);
        Assert.Contains(weapon.ItemId, (Guid[])[first, second]);
    }

    [Fact]
    public async Task Two_swords_racing_for_an_unnamed_hand_do_not_both_take_the_first_one()
    {
        using var client = await factory.SignedInAsync("race-default-slot", Cancellation);
        var first = await client.ForgeSwordAsync(factory, Cancellation);
        var second = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        // Both requests can see two empty hands and both choose the first. At most one of them
        // can have it; whether the other lands in the second hand or is turned away, the unit
        // must never end up with two weapons in one hand.
        await Task.WhenAll(
            client.PostEquipAsync(melee.Id, first, Cancellation),
            client.PostEquipAsync(melee.Id, second, Cancellation));

        var unit = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        var occupied = unit.Weapons.SelectMany(weapon => weapon.Slots).ToList();

        Assert.Equal(occupied.Count, occupied.Distinct().Count());
        Assert.All(occupied, slot => Assert.InRange(slot, 1, unit.WeaponSlots));
    }

    [Fact]
    public async Task A_duplicated_equip_request_leaves_one_weapon_in_one_hand()
    {
        using var client = await factory.SignedInAsync("race-duplicate", Cancellation);
        var sword = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);

        var answers = await Task.WhenAll(
            client.PostEquipAsync(melee.Id, sword, Cancellation, slot: 2),
            client.PostEquipAsync(melee.Id, sword, Cancellation, slot: 2));

        Assert.Single(answers, answer => answer.IsSuccessStatusCode);
        Assert.Equal([2], (await client.UnitAsync(PreparationApi.MeleeKey, Cancellation)).SlotsOf(sword));
    }

    [Fact]
    public async Task A_duplicated_unequip_request_is_coherent()
    {
        using var client = await factory.SignedInAsync("race-unequip", Cancellation);
        var sword = await client.ForgeSwordAsync(factory, Cancellation);
        var melee = await client.UnitAsync(PreparationApi.MeleeKey, Cancellation);
        await client.EquipAsync(melee.Id, sword, Cancellation);

        await Task.WhenAll(
            client.PostUnequipAsync(melee.Id, sword, Cancellation),
            client.PostUnequipAsync(melee.Id, sword, Cancellation));

        Assert.Empty((await client.UnitAsync(PreparationApi.MeleeKey, Cancellation)).Weapons);
        Assert.Null(Assert.Single(await client.ReadInventoryAsync(Cancellation)).EquippedOn);
    }

    private async Task<int> CountEquipmentAsync(
        System.Linq.Expressions.Expression<Func<WeaponsOfOrder.Infrastructure.Gameplay.EquippedWeapon, bool>> predicate)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeaponsOfOrderDbContext>();

        return await db.EquippedWeapons.AsNoTracking().CountAsync(predicate, Cancellation);
    }
}
