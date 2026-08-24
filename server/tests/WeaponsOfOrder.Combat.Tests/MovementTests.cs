using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// Pursuit, collision and the fact that a Unit walks rather than appears.
/// </summary>
public class MovementTests
{
    /// <summary>Two melee Units on opposite edges close the distance and meet in the middle.</summary>
    [Fact]
    public void A_Unit_out_of_range_pursues_its_target()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(0, 3), Fight.Stats(hp: 10_000, power: 1));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("foe", new Hex(7, 3), Fight.Stats(hp: 10_000, power: 1));

        Assert.Equal(7, new Hex(0, 3).DistanceTo(new Hex(7, 3)));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        BattleInvariants.AssertConsistent(result);

        var advance = result.MovesBy("blade").ToList();
        Assert.NotEmpty(advance);
        Assert.NotEmpty(result.MovesBy("foe"));

        // The first step is towards the enemy rather than anywhere else on the board.
        Assert.Equal(6, advance[0].To.DistanceTo(new Hex(7, 3)));

        Assert.NotEmpty(result.AttacksBy("blade"));
    }

    /// <summary>
    /// A ranged Unit stops as soon as its target is in reach and does not walk into melee.
    /// </summary>
    /// <remarks>
    /// The enemy is given reach across the whole board so it never moves, which leaves the
    /// archer's stopping distance as the only thing the test is measuring.
    /// </remarks>
    [Fact]
    public void A_ranged_Unit_stops_at_its_range()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("archer", new Hex(0, 3), Fight.Stats(hp: 1_000_000, power: 1, range: 3));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("anchor", new Hex(7, 3), Fight.Stats(hp: 1_000_000, power: 1, range: 7));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 8)));

        BattleInvariants.AssertConsistent(result);

        Assert.Empty(result.MovesBy("anchor"));

        var finished = result.Combatant("archer").FinalHex;
        Assert.NotNull(finished);
        Assert.Equal(3, finished.Value.DistanceTo(new Hex(7, 3)));
    }

    /// <summary>The same board walked twice produces the same route, step for step.</summary>
    [Fact]
    public void Path_choice_is_deterministic()
    {
        static IReadOnlyList<Hex> Walk()
        {
            var player = new ArmyUnderTest("player")
                .Deploy("blade", new Hex(0, 0), Fight.Stats(hp: 10_000, power: 1))
                .Deploy("second", new Hex(0, 1), Fight.Stats(hp: 10_000, power: 1))
                .Deploy("third", new Hex(1, 4), Fight.Stats(hp: 10_000, power: 1));

            var opponent = new ArmyUnderTest("opponent")
                .Deploy("foe", new Hex(7, 6), Fight.Stats(hp: 10_000, power: 1))
                .Deploy("other", new Hex(6, 2), Fight.Stats(hp: 10_000, power: 1));

            var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

            return [.. result.MovesBy("blade").Select(move => move.To)];
        }

        var routes = Enumerable.Range(0, 5).Select(_ => Walk()).ToList();

        Assert.All(routes, route => Assert.Equal(routes[0], route));
        Assert.NotEmpty(routes[0]);
    }

    /// <summary>
    /// A Mounted Unit covers the same ground sooner.
    /// </summary>
    /// <remarks>
    /// Canon's one inherent movement distinction for v1. Two otherwise identical Units start the
    /// same distance out, so the only thing that can separate their arrival is the mount.
    /// </remarks>
    [Fact]
    public void A_Mounted_Unit_takes_its_steps_sooner()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("rider", new Hex(0, 1), Fight.Stats(hp: 10_000, power: 1, mounted: true))
            .Deploy("walker", new Hex(0, 5), Fight.Stats(hp: 10_000, power: 1));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("mark-a", new Hex(7, 1), Fight.Stats(hp: 10_000, power: 1, range: 7))
            .Deploy("mark-b", new Hex(7, 5), Fight.Stats(hp: 10_000, power: 1, range: 7));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        BattleInvariants.AssertConsistent(result);

        // The second step, not the first: both are ready to move the instant the battle starts, so
        // the mount only shows once an interval has had to pass.
        var rider = result.MovesBy("rider").Skip(1).First().TimeMilliseconds;
        var walker = result.MovesBy("walker").Skip(1).First().TimeMilliseconds;

        Assert.Equal(0, result.MovesBy("rider").First().TimeMilliseconds);
        Assert.Equal(0, result.MovesBy("walker").First().TimeMilliseconds);
        Assert.True(rider < walker, $"The rider stepped again at {rider} and the Unit on foot at {walker}.");
    }

    /// <summary>
    /// A crowded battlefield never lets two Units share a hex or walk through one another.
    /// </summary>
    /// <remarks>
    /// Eight against eight with reserves behind both, which is the largest battle the canonical
    /// limits allow. The invariants are replayed from the log rather than asserted at the end,
    /// because a collision on one tick of a long fight leaves no trace in a final snapshot.
    /// </remarks>
    [Fact]
    public void Units_never_pass_through_one_another_in_a_full_battle()
    {
        var player = new ArmyUnderTest("player");
        var opponent = new ArmyUnderTest("opponent");

        for (var index = 0; index < 8; index++)
        {
            var row = index % 7;
            var column = index < 7 ? 3 : 2;

            player.Deploy($"p{index}", new Hex(column, row), Fight.Stats(hp: 400, power: 4, defense: 10));
            opponent.Deploy($"o{index}", new Hex(7 - column, row), Fight.Stats(hp: 400, power: 4, defense: 10));

            player.Reserve($"pr{index}", Fight.Stats(hp: 400, power: 4, defense: 10));
            opponent.Reserve($"or{index}", Fight.Stats(hp: 400, power: 4, defense: 10));
        }

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 60)));

        BattleInvariants.AssertConsistent(result);
        Assert.NotEmpty(result.EventsOf<CombatantMoved>());
        Assert.NotEmpty(result.EventsOf<ReserveEntered>());
    }
}
