using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// Reinforcement: the delay, the assigned entry hex, and what happens when it is occupied.
/// </summary>
/// <remarks>
/// The tests here run with a deliberately small active limit, because the rule is about slots
/// opening and closing rather than about eight of anything.
/// </remarks>
public class ReserveTests
{
    /// <summary>A slot opens, and after the configured delay the reserve walks in at its own hex.</summary>
    [Fact]
    public void A_reserve_enters_through_its_assigned_hex_after_the_delay()
    {
        var tuning = Fight.Quick(maximumSeconds: 30, noProgressSeconds: 20) with { ActiveLimit = 1 };

        var player = new ArmyUnderTest("player")

            // Exactly one opening blow's worth of HP, so the slot opens at time zero.
            .Deploy("vanguard", new Hex(3, 3), Fight.Stats(hp: 50))
            .Reserve("second", Fight.Stats(hp: 500));

        var opponent = new ArmyUnderTest("opponent").Deploy("foe", new Hex(4, 3), Fight.Stats(hp: 500));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, tuning));

        BattleInvariants.AssertConsistent(result);

        var second = result.Combatant("second");
        Assert.Equal(0, second.ReserveOrder);
        Assert.Equal(new Hex(0, 0), second.ReserveEntryHex);

        var entry = Assert.Single(result.EventsOf<ReserveEntered>());
        Assert.Equal(second.Id, entry.Id);
        Assert.Equal(new Hex(0, 0), entry.Hex);

        // Not the instant the slot opened: canon asks for a short configurable delay, and this is it.
        Assert.Equal(0, result.EventsOf<CombatantDied>().First().TimeMilliseconds);
        Assert.Equal(tuning.ReserveEntryDelayMilliseconds, entry.TimeMilliseconds);

        // And it fought, rather than merely appearing.
        Assert.NotEmpty(result.AttacksBy("second"));
    }

    /// <summary>
    /// A reserve whose assigned hex is taken waits off-board, alive, and enters when it clears.
    /// </summary>
    /// <remarks>
    /// Two reserves share one entry hex here, which is what the wrapping assignment allows on a
    /// board one row deep. The second is refused at its first attempt and takes the next one.
    /// <para>
    /// It also proves the harder half of the defeat rule: between the first reserve dying and the
    /// second arriving, the player has nothing at all on the battlefield and is still not defeated.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_blocked_reserve_waits_alive_and_enters_when_its_hex_clears()
    {
        var field = new Battlefield(2, 1);
        var tuning = Fight.Quick(maximumSeconds: 30, noProgressSeconds: 20) with { ActiveLimit = 2 };

        var player = new ArmyUnderTest("player")
            .Reserve("first", Fight.Stats(hp: 50, power: 1))
            .Reserve("blocked", Fight.Stats(hp: 100, power: 1));

        var opponent = new ArmyUnderTest("opponent").Reserve("foe", Fight.Stats(hp: 10_000, power: 10));

        Assert.Equal(new Hex(0, 0), field.ReserveEntryHex(BattleSide.Player, 0));
        Assert.Equal(new Hex(0, 0), field.ReserveEntryHex(BattleSide.Player, 1));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, tuning, field));

        BattleInvariants.AssertConsistent(result);

        var delay = tuning.ReserveEntryDelayMilliseconds;
        var entries = result.EventsOf<ReserveEntered>().ToList();

        var first = entries.Single(entered => entered.Id == result.Id("first"));
        var blocked = entries.Single(entered => entered.Id == result.Id("blocked"));

        Assert.Equal(delay, first.TimeMilliseconds);
        Assert.Equal(new Hex(0, 0), first.Hex);

        // Called at the same moment as the first and refused, because the hex it must use was taken.
        // It entered at a later attempt, through that same hex and no other.
        Assert.Equal(new Hex(0, 0), blocked.Hex);
        Assert.True(
            blocked.TimeMilliseconds > first.TimeMilliseconds,
            $"The blocked reserve entered at {blocked.TimeMilliseconds}, not after {first.TimeMilliseconds}.");

        // The battlefield was empty of player Units between these two moments, and the battle
        // carried on regardless.
        var fell = result.EventsOf<CombatantDied>().Single(died => died.Id == result.Id("first"));
        Assert.True(fell.TimeMilliseconds < blocked.TimeMilliseconds);
        Assert.True(result.EventsOf<BattleEnded>().Single().TimeMilliseconds > fell.TimeMilliseconds);
    }

    /// <summary>
    /// A reserve that can never enter keeps the army alive and never counts as progress.
    /// </summary>
    /// <remarks>
    /// Two Units stand adjacent with an Attack Interval longer than the whole battle, so each lands
    /// exactly one blow and then nothing happens again. The third Unit's entry hex is permanently
    /// occupied by its own ally, and it retries every two seconds for the rest of the battle.
    /// <para>
    /// If a failed attempt counted as progress, those retries would hold the no-progress window
    /// open until the hard duration cap. The battle instead ends one window after the last real
    /// event, which is the assertion this test exists for.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_failed_entry_attempt_is_not_progress()
    {
        var field = new Battlefield(2, 1);
        var tuning = Fight.Quick(maximumSeconds: 60, noProgressSeconds: 10) with { ActiveLimit = 2 };

        var still = Fight.Stats(hp: 10_000, interval: 3_600);

        var player = new ArmyUnderTest("player")
            .Reserve("holder", still)
            .Reserve("stranded", still);

        var opponent = new ArmyUnderTest("opponent").Reserve("foe", still);

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, tuning, field));

        BattleInvariants.AssertConsistent(result);

        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.Equal(BattleEndReason.NoProgress, result.Reason);

        var stranded = result.Combatant("stranded");
        Assert.Equal(CombatantEndState.Reserve, stranded.EndState);
        Assert.Equal(10_000, stranded.FinalHp);
        Assert.DoesNotContain(result.EventsOf<ReserveEntered>(), entered => entered.Id == stranded.Id);

        // The last thing that actually happened, and the guard firing exactly one window later.
        var lastRealEvent = result.Events
            .Where(moment => moment is not BattleEnded)
            .Max(moment => moment.TimeMilliseconds);

        Assert.Equal(lastRealEvent + tuning.NoProgressMilliseconds, result.DurationMilliseconds);
        Assert.True(
            result.DurationMilliseconds < tuning.MaximumDurationMilliseconds,
            "The battle ran to the hard cap, so the failed attempts were resetting the window.");
    }

    /// <summary>A stalemate Draw leaves the survivors and the blocked reserve exactly as they were.</summary>
    [Fact]
    public void A_guard_Draw_does_not_kill_anybody()
    {
        var field = new Battlefield(2, 1);
        var tuning = Fight.Quick(maximumSeconds: 60, noProgressSeconds: 10) with { ActiveLimit = 2 };

        var still = Fight.Stats(hp: 10_000, interval: 3_600);

        var player = new ArmyUnderTest("player").Reserve("holder", still).Reserve("stranded", still);
        var opponent = new ArmyUnderTest("opponent").Reserve("foe", still);

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, tuning, field));

        Assert.Empty(result.EventsOf<CombatantDied>());
        Assert.All(result.Combatants, combatant => Assert.True(combatant.FinalHp > 0));

        Assert.Equal(CombatantEndState.Active, result.Combatant("holder").EndState);
        Assert.Equal(CombatantEndState.Active, result.Combatant("foe").EndState);
        Assert.Equal(CombatantEndState.Reserve, result.Combatant("stranded").EndState);
    }
}
