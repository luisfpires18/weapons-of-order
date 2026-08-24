using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// The single Energy bar, as a battle actually fills and spends it.
/// </summary>
/// <remarks>
/// <see cref="DamageMathTests"/> asserts the arithmetic. This asserts that a Unit fighting for
/// eleven seconds walks up the bar one auto at a time and spends it on the eleventh.
/// </remarks>
public class EnergyTests
{
    [Fact]
    public void Ten_autos_fill_the_bar_and_the_eleventh_attack_is_the_Heavy()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 1_000_000, power: 10));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("dummy", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var result = BattleSimulator.Simulate(
            Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 12, noProgressSeconds: 11)));

        var attacks = result.AttacksBy("blade").Take(11).ToList();
        Assert.Equal(11, attacks.Count);

        var autos = attacks.Take(10).ToList();
        Assert.All(autos, attack => Assert.Equal(AttackKind.Normal, attack.Kind));
        Assert.Equal(
            [10, 20, 30, 40, 50, 60, 70, 80, 90, 100],
            autos.Select(attack => attack.AttackerEnergyAfter));

        // Each of them is a plain 50: ten Power, five raw damage a point, no Defense in the way.
        Assert.All(autos, attack => Assert.Equal(50, attack.Damage));

        var heavy = attacks[10];
        Assert.Equal(AttackKind.Heavy, heavy.Kind);
        Assert.Equal(125, heavy.Damage);
        Assert.Equal(0, heavy.AttackerEnergyAfter);
    }

    /// <summary>The bar tops out rather than banking the overflow towards the next Heavy.</summary>
    [Fact]
    public void Energy_never_reads_above_the_bar()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 1_000_000, power: 10, interval: 0.5));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("dummy", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var result = BattleSimulator.Simulate(
            Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 20, noProgressSeconds: 19)));

        Assert.All(
            result.EventsOf<AttackResolved>(),
            attack => Assert.InRange(attack.AttackerEnergyAfter, 0, 100));

        // Full bars are spent, not stockpiled: every Heavy leaves the attacker empty.
        var heavies = result.AttacksBy("blade").Where(attack => attack.Kind == AttackKind.Heavy).ToList();
        Assert.NotEmpty(heavies);
        Assert.All(heavies, attack => Assert.Equal(0, attack.AttackerEnergyAfter));
    }

    /// <summary>A critical Heavy is the canon's worked example: 50, 125, 250.</summary>
    [Fact]
    public void A_Heavy_attack_can_crit()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 1_000_000, power: 10, crit: 1.0));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("dummy", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var result = BattleSimulator.Simulate(
            Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 12, noProgressSeconds: 11)));

        var attacks = result.AttacksBy("blade").Take(11).ToList();

        Assert.All(attacks, attack => Assert.True(attack.Critical));
        Assert.All(attacks.Take(10), attack => Assert.Equal(100, attack.Damage));
        Assert.Equal(250, attacks[10].Damage);
    }
}
