using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// How a battle stops, and the guarantee that it always does.
/// </summary>
public class TerminationTests
{
    [Fact]
    public void An_army_with_nothing_left_alive_has_lost()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 400, power: 20));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("foe", new Hex(4, 3), Fight.Stats(hp: 100, power: 2))
            .Reserve("reinforcement", Fight.Stats(hp: 100, power: 2));

        var result = BattleSimulator.Simulate(
            Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 40, noProgressSeconds: 20)));

        BattleInvariants.AssertConsistent(result);

        Assert.Equal(BattleOutcome.PlayerVictory, result.Outcome);
        Assert.Equal(BattleEndReason.Elimination, result.Reason);

        // Both of them, not only the one that started on the board. An army is defeated when every
        // Unit belonging to the battle is dead, reserves included.
        Assert.Equal(CombatantEndState.Dead, result.Combatant("foe").EndState);
        Assert.Equal(CombatantEndState.Dead, result.Combatant("reinforcement").EndState);
        Assert.Equal(CombatantEndState.Active, result.Combatant("blade").EndState);
    }

    /// <summary>
    /// A fight that is going nowhere in particular still ends when the hard cap expires.
    /// </summary>
    /// <remarks>
    /// Both Units keep landing blows, so the no-progress window resets every second and can never
    /// fire. The hard cap exists precisely for that: cyclic combat must not be able to run forever
    /// by being productive.
    /// </remarks>
    [Fact]
    public void The_hard_duration_cap_ends_a_battle_that_will_not_finish()
    {
        var tuning = Fight.Quick(maximumSeconds: 5, noProgressSeconds: 30);

        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("foe", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 1));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, tuning));

        BattleInvariants.AssertConsistent(result);

        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.Equal(BattleEndReason.MaximumDuration, result.Reason);
        Assert.Equal(tuning.MaximumDurationMilliseconds, result.DurationMilliseconds);

        // The guard is a stopwatch, not a scythe: both are alive and hurt exactly as much as the
        // blows they took.
        Assert.Empty(result.EventsOf<CombatantDied>());
        Assert.All(result.Combatants, combatant => Assert.Equal(CombatantEndState.Active, combatant.EndState));
    }

    /// <summary>
    /// Combat that stops happening ends on the no-progress window rather than waiting out the cap.
    /// </summary>
    /// <remarks>
    /// Two Units adjacent, each with an Attack Interval longer than the battle. One blow each, and
    /// then a stand-off that nothing will ever break.
    /// </remarks>
    [Fact]
    public void The_no_progress_window_ends_a_stand_off()
    {
        var tuning = Fight.Quick(maximumSeconds: 120, noProgressSeconds: 6);

        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 10_000, interval: 3_600));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("foe", new Hex(4, 3), Fight.Stats(hp: 10_000, interval: 3_600));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, tuning));

        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.Equal(BattleEndReason.NoProgress, result.Reason);

        // One exchange at time zero, then the window runs out from there.
        Assert.Equal(2, result.EventsOf<AttackResolved>().Count());
        Assert.Equal(tuning.NoProgressMilliseconds, result.DurationMilliseconds);
    }

    /// <summary>
    /// Nothing anyone can deploy makes the simulator run forever.
    /// </summary>
    /// <remarks>
    /// A battery of the shapes most likely to cycle: Units that can barely hurt each other, Units
    /// that never get to attack, a board packed to both deployment limits with every reserve queued
    /// behind it, and a Unit walled in behind its own front line. Every one of them has to come back
    /// inside the cap.
    /// </remarks>
    [Fact]
    public void Every_battle_terminates()
    {
        foreach (var (description, input) in PathologicalBattles())
        {
            var result = BattleSimulator.Simulate(input);

            BattleInvariants.AssertConsistent(result);

            Assert.True(
                result.DurationMilliseconds <= input.Tuning.MaximumDurationMilliseconds,
                $"'{description}' ran to {result.DurationMilliseconds}ms.");

            Assert.Single(result.EventsOf<BattleEnded>());
        }
    }

    private static IEnumerable<(string Description, BattleInput Input)> PathologicalBattles()
    {
        var tuning = Fight.Quick(maximumSeconds: 30, noProgressSeconds: 8);

        yield return (
            "two Units that can barely scratch each other",
            Fight.Between(
                new ArmyUnderTest("player").Deploy("a", new Hex(3, 3), Fight.Stats(hp: 1_000_000, power: 0)),
                new ArmyUnderTest("opponent").Deploy("b", new Hex(4, 3), Fight.Stats(hp: 1_000_000, power: 0)),
                tuning));

        yield return (
            "two Units whose Attack Interval outlasts the battle",
            Fight.Between(
                new ArmyUnderTest("player").Deploy("a", new Hex(0, 0), Fight.Stats(interval: 100_000)),
                new ArmyUnderTest("opponent").Deploy("b", new Hex(7, 6), Fight.Stats(interval: 100_000)),
                tuning));

        var full = new ArmyUnderTest("player");
        var against = new ArmyUnderTest("opponent");

        for (var index = 0; index < 8; index++)
        {
            var stats = Fight.Stats(hp: 100_000, power: 0, defense: 500);

            full.Deploy($"p{index}", new Hex(index < 7 ? 3 : 2, index % 7), stats);
            against.Deploy($"o{index}", new Hex(index < 7 ? 4 : 5, index % 7), stats);
            full.Reserve($"pr{index}", stats);
            against.Reserve($"or{index}", stats);
        }

        yield return ("both deployment limits filled, every reserve queued behind them", Fight.Between(full, against, tuning));

        var jailed = new ArmyUnderTest("player");
        var jailers = new ArmyUnderTest("opponent");

        // A wall of bodies down the middle. The Unit behind the front line has nowhere to go and
        // nothing it can reach, which is the melee jail canon deliberately permits.
        for (var row = 0; row < 7; row++)
        {
            jailed.Deploy($"line{row}", new Hex(3, row), Fight.Stats(hp: 100_000, power: 0, defense: 900));
            jailers.Deploy($"wall{row}", new Hex(4, row), Fight.Stats(hp: 100_000, power: 0, defense: 900));
        }

        jailed.Deploy("stuck", new Hex(2, 3), Fight.Stats(hp: 100_000, power: 0));

        yield return ("a Unit walled in behind its own front line", Fight.Between(jailed, jailers, tuning));
    }
}
