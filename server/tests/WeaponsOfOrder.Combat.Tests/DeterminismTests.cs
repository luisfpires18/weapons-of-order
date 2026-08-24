using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// The property the whole architecture rests on: one input, one battle, every time.
/// </summary>
/// <remarks>
/// Without this, a replay is a re-roll, an asynchronous defence resolves differently for each
/// side, and nothing the server says about a battle can be checked afterwards.
/// </remarks>
public class DeterminismTests
{
    /// <summary>A battle busy enough that a wobble anywhere in it would show.</summary>
    private static BattleInput Melee(long seed)
    {
        var player = new ArmyUnderTest("player");
        var opponent = new ArmyUnderTest("opponent");

        for (var index = 0; index < 6; index++)
        {
            player.Deploy(
                $"p{index}",
                new Hex(index % 2 == 0 ? 3 : 2, index),
                Fight.Stats(hp: 260, power: 6 + index, defense: index * 4, crit: 0.35, range: index == 5 ? 3 : 1));

            opponent.Deploy(
                $"o{index}",
                new Hex(index % 2 == 0 ? 4 : 5, index),
                Fight.Stats(hp: 260, power: 7, defense: 12, crit: 0.35, range: index == 0 ? 3 : 1));

            player.Reserve($"pr{index}", Fight.Stats(hp: 200, power: 6, crit: 0.35));
            opponent.Reserve($"or{index}", Fight.Stats(hp: 200, power: 6, crit: 0.35, movementSpeed: index % 2 == 0 ? 1.4 : 1.0));
        }

        return Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 60, noProgressSeconds: 12), seed: seed);
    }

    [Fact]
    public void The_same_input_produces_the_same_battle_every_time()
    {
        var input = Melee(seed: 987_654_321);

        var transcripts = Enumerable.Range(0, 4)
            .Select(_ => BattleSimulator.Simulate(input).Transcript())
            .ToList();

        Assert.All(transcripts, transcript => Assert.Equal(transcripts[0], transcript));

        // Not a battle that ended before anything happened.
        Assert.True(transcripts[0].Count > 100, $"The reference battle produced only {transcripts[0].Count} lines.");
    }

    /// <summary>Two inputs that differ only by seed have to differ, or the seed is doing nothing.</summary>
    [Fact]
    public void A_different_seed_produces_a_different_battle()
    {
        var one = BattleSimulator.Simulate(Melee(seed: 1)).Transcript();
        var other = BattleSimulator.Simulate(Melee(seed: 2)).Transcript();

        Assert.NotEqual(one, other);
    }

    [Fact]
    public void A_Unit_with_no_Critical_Chance_never_crits()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 1_000_000, interval: 0.4, crit: 0));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("dummy", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var result = BattleSimulator.Simulate(
            Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 20, noProgressSeconds: 19)));

        var attacks = result.AttacksBy("blade").ToList();

        Assert.True(attacks.Count > 30, $"Only {attacks.Count} attacks were rolled.");
        Assert.All(attacks, attack => Assert.False(attack.Critical));
    }

    [Fact]
    public void A_Unit_that_always_crits_always_crits()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 1_000_000, interval: 0.4, crit: 1));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("dummy", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var result = BattleSimulator.Simulate(
            Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 20, noProgressSeconds: 19)));

        Assert.All(result.AttacksBy("blade"), attack => Assert.True(attack.Critical));
    }

    /// <summary>
    /// A coin-flip Critical Chance behaves like one.
    /// </summary>
    /// <remarks>
    /// The generator is counter-based, so every roll is a hash of the moment it belongs to rather
    /// than the next number in a stream. That is what keeps the roster's iteration order out of the
    /// dice — but it would be worth nothing if the hash were biased, so the distribution is checked
    /// as well as the reproducibility.
    /// </remarks>
    [Fact]
    public void A_half_chance_crits_about_half_the_time()
    {
        var random = new DeterministicRandom(20260823);

        var hits = 0;
        const int Rolls = 20_000;

        for (var roll = 0; roll < Rolls; roll++)
        {
            if (random.Chance(0.5, roll * 50, new CombatantId(BattleSide.Player, roll % 16), DeterministicRandom.Purpose.Critical, roll))
            {
                hits++;
            }
        }

        Assert.InRange(hits / (double)Rolls, 0.47, 0.53);
    }

    /// <summary>The same roll asked for twice answers the same, and neighbouring rolls do not.</summary>
    [Fact]
    public void One_roll_is_one_answer()
    {
        var random = new DeterministicRandom(7);
        var moment = random.Next(1_000, new CombatantId(BattleSide.Player, 3), DeterministicRandom.Purpose.Critical, 5);

        Assert.Equal(moment, random.Next(1_000, new CombatantId(BattleSide.Player, 3), DeterministicRandom.Purpose.Critical, 5));

        Assert.NotEqual(moment, random.Next(1_050, new CombatantId(BattleSide.Player, 3), DeterministicRandom.Purpose.Critical, 5));
        Assert.NotEqual(moment, random.Next(1_000, new CombatantId(BattleSide.Opponent, 3), DeterministicRandom.Purpose.Critical, 5));
        Assert.NotEqual(moment, random.Next(1_000, new CombatantId(BattleSide.Player, 4), DeterministicRandom.Purpose.Critical, 5));
        Assert.NotEqual(moment, random.Next(1_000, new CombatantId(BattleSide.Player, 3), DeterministicRandom.Purpose.Critical, 6));
        Assert.NotEqual(moment, new DeterministicRandom(8).Next(1_000, new CombatantId(BattleSide.Player, 3), DeterministicRandom.Purpose.Critical, 5));
    }

    [Fact]
    public void Every_roll_lands_inside_the_unit_interval()
    {
        var random = new DeterministicRandom(-4_611_686_018_427_387_904);

        for (var roll = 0; roll < 5_000; roll++)
        {
            var value = random.Next(roll, new CombatantId(BattleSide.Opponent, roll % 32), DeterministicRandom.Purpose.Critical, roll);

            Assert.InRange(value, 0, 0.9999999999999999);
        }
    }
}
