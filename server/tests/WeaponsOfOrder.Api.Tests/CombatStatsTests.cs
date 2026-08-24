using Xunit;

namespace WeaponsOfOrder.Api.Tests;

/// <summary>
/// The additive stat model, from a real forged sword to the numbers a battle is fought with.
/// </summary>
/// <remarks>
/// The weapon is forged and equipped through the real APIs rather than inserted, because the
/// claim being tested is that what the forge makes is what a Unit fights with.
/// <para>
/// The Unit's and weapon's values are pinned by <see cref="KnownStatsApiFactory"/>. Asserting
/// against the creator's own balance numbers would mean a balance edit breaks a test about
/// addition.
/// </para>
/// </remarks>
public class CombatStatsTests(KnownStatsApiFactory factory)
    : IClassFixture<KnownStatsApiFactory>, IAsyncLifetime
{
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => factory.InitializeAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>A Unit holding nothing fights with its own stats and the unarmed fallback's reach.</summary>
    [Fact]
    public async Task An_empty_handed_Unit_has_its_own_stats()
    {
        using var client = await factory.SignedInAsync("stats-unarmed", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        Assert.Equal(KnownStatsApiFactory.UnitHp, unit.Stats.Hp);
        Assert.Equal(KnownStatsApiFactory.UnitPower, unit.Stats.Power);
        Assert.Equal(KnownStatsApiFactory.UnitDefense, unit.Stats.Defense);
        Assert.Equal(KnownStatsApiFactory.UnitAttackInterval, unit.Stats.AttackIntervalSeconds, 6);
        Assert.Equal(KnownStatsApiFactory.UnitCriticalChance, unit.Stats.CriticalChance, 6);

        // Reach comes from what is in the Unit's hands. With nothing in them, it is the configured
        // unarmed fallback rather than anything derived from what the Unit is called.
        Assert.Equal(1, unit.Stats.Range);
    }

    [Fact]
    public async Task An_equipped_weapon_adds_its_Power_reach_crit_and_weight()
    {
        using var client = await factory.SignedInAsync("stats-armed", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);
        var sword = await client.ForgeSwordAsync(factory, Token);

        await client.EquipAsync(unit.UnitId, sword, Token);

        var armed = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        Assert.Equal(KnownStatsApiFactory.UnitPower + KnownStatsApiFactory.WeaponPower, armed.Stats.Power);
        Assert.Equal(
            KnownStatsApiFactory.UnitCriticalChance + KnownStatsApiFactory.WeaponCriticalChance,
            armed.Stats.CriticalChance,
            6);
        Assert.Equal(
            KnownStatsApiFactory.UnitAttackInterval + KnownStatsApiFactory.WeaponIntervalCost,
            armed.Stats.AttackIntervalSeconds,
            6);
        Assert.Equal(KnownStatsApiFactory.WeaponRange, armed.Stats.Range);

        // HP and Defense are the Unit's own. Weapons carry neither, and no armour exists.
        Assert.Equal(KnownStatsApiFactory.UnitHp, armed.Stats.Hp);
        Assert.Equal(KnownStatsApiFactory.UnitDefense, armed.Stats.Defense);

        Assert.Single(armed.Weapons);
        Assert.Equal(sword, armed.Weapons[0].ItemId);
    }

    /// <summary>
    /// Both hands of a two-item loadout contribute in full.
    /// </summary>
    /// <remarks>
    /// The weapon registry is explicit that the second slot is a full weapon slot with no off-hand
    /// penalty, and that both equipped 1-slot weapons contribute 100% of their stats.
    /// </remarks>
    [Fact]
    public async Task Two_one_slot_weapons_both_contribute_in_full()
    {
        using var client = await factory.SignedInAsync("stats-dual", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        await client.EquipAsync(unit.UnitId, await client.ForgeSwordAsync(factory, Token), Token);
        await client.EquipAsync(unit.UnitId, await client.ForgeSwordAsync(factory, Token), Token);

        var armed = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);

        Assert.Equal(2, armed.Weapons.Count);
        Assert.Equal(KnownStatsApiFactory.UnitPower + (2 * KnownStatsApiFactory.WeaponPower), armed.Stats.Power);
        Assert.Equal(
            KnownStatsApiFactory.UnitCriticalChance + (2 * KnownStatsApiFactory.WeaponCriticalChance),
            armed.Stats.CriticalChance,
            6);
        Assert.Equal(
            KnownStatsApiFactory.UnitAttackInterval + (2 * KnownStatsApiFactory.WeaponIntervalCost),
            armed.Stats.AttackIntervalSeconds,
            6);
    }

    /// <summary>Taking the weapon away takes its contribution with it.</summary>
    [Fact]
    public async Task Unequipping_returns_a_Unit_to_its_own_stats()
    {
        using var client = await factory.SignedInAsync("stats-unequip", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);
        var before = unit.Stats;
        var sword = await client.ForgeSwordAsync(factory, Token);

        await client.EquipAsync(unit.UnitId, sword, Token);
        Assert.NotEqual(before, (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey).Stats);

        await client.UnequipAsync(unit.UnitId, sword, Token);
        Assert.Equal(before, (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey).Stats);
    }

    /// <summary>The stats a battle is fought with are the ones the deployment screen showed.</summary>
    [Fact]
    public async Task A_battle_is_fought_with_the_stats_the_army_publishes()
    {
        using var client = await factory.SignedInAsync("stats-battle", Token);

        var unit = (await client.ReadArmyAsync(Token)).Unit(PreparationApi.MeleeKey);
        await client.EquipAsync(unit.UnitId, await client.ForgeSwordAsync(factory, Token), Token);

        var army = await client.ReadArmyAsync(Token);
        await client.DeployEveryUnitAsync(army, Token);

        var result = await client.SimulateAsync(Token);

        foreach (var expected in army.Units)
        {
            var fighting = Assert.Single(result.Combatants, combatant => combatant.UnitId == expected.UnitId);

            Assert.Equal(expected.Stats, fighting.Stats);
        }
    }
}
