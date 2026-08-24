using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// The rule that everything else in the timing model exists to protect.
/// </summary>
/// <remarks>
/// Attacks sharing a timestamp are one batch. Every attack already committed for that timestamp
/// lands, including the one belonging to a Unit that dies in the same batch, and none of them
/// gets first-strike survival priority for having been serialised first. A mutual last kill is
/// therefore a Draw and not a race between two list positions.
/// </remarks>
public class SimultaneousTimestampTests
{
    /// <summary>Two Units, adjacent, ready at the same instant, both landing a blow at time zero.</summary>
    [Fact]
    public void Attacks_at_the_same_timestamp_both_resolve()
    {
        var player = new ArmyUnderTest("player").Deploy("blade", new Hex(3, 3), Fight.Stats(hp: 500));
        var opponent = new ArmyUnderTest("opponent").Deploy("foe", new Hex(4, 3), Fight.Stats(hp: 500));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        var opening = result.EventsOf<AttackResolved>().Where(attack => attack.TimeMilliseconds == 0).ToList();

        Assert.Equal(2, opening.Count);
        Assert.Contains(opening, attack => attack.AttackerId == result.Id("blade"));
        Assert.Contains(opening, attack => attack.AttackerId == result.Id("foe"));
    }

    /// <summary>
    /// A Unit killed at time T still lands the attack it had committed for time T.
    /// </summary>
    /// <remarks>
    /// Each has exactly the HP the other's opening blow removes: 10 Power is 50 raw damage, and
    /// neither carries any Defense. If either attack were dropped because its attacker had already
    /// been killed, one army would survive and the result would be a victory.
    /// </remarks>
    [Fact]
    public void A_mutual_last_kill_is_a_Draw()
    {
        var lethal = DamageMath.Damage(10, AttackKind.Normal, critical: false, defense: 0, CombatTuning.Default);
        Assert.Equal(50, lethal);

        var player = new ArmyUnderTest("player").Deploy("blade", new Hex(3, 3), Fight.Stats(hp: lethal));
        var opponent = new ArmyUnderTest("opponent").Deploy("foe", new Hex(4, 3), Fight.Stats(hp: lethal));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        Assert.Equal(BattleOutcome.Draw, result.Outcome);
        Assert.Equal(BattleEndReason.MutualElimination, result.Reason);
        Assert.Equal(0, result.DurationMilliseconds);

        // Both blows landed, and both Units died of them.
        Assert.Equal(2, result.EventsOf<AttackResolved>().Count());
        Assert.Equal(2, result.EventsOf<CombatantDied>().Count());
        Assert.All(result.Combatants, combatant => Assert.Equal(CombatantEndState.Dead, combatant.EndState));

        BattleInvariants.AssertConsistent(result);
    }

    /// <summary>
    /// Neither side is favoured by being the side it is.
    /// </summary>
    /// <remarks>
    /// The same fight with the armies swapped has to produce the mirrored result. If the simulator
    /// gave the player's roster any advantage — resolving first, dying last — this is where it
    /// would show.
    /// </remarks>
    [Fact]
    public void Swapping_the_armies_mirrors_the_result()
    {
        static BattleResult Run(bool swapped)
        {
            var strong = Fight.Stats(hp: 300, power: 12);
            var weak = Fight.Stats(hp: 200, power: 8);

            var one = new ArmyUnderTest("one").Deploy("strong", new Hex(3, 3), swapped ? weak : strong);
            var two = new ArmyUnderTest("two").Deploy("weak", new Hex(4, 3), swapped ? strong : weak);

            return BattleSimulator.Simulate(Fight.Between(one, two, Fight.Quick()));
        }

        var normal = Run(swapped: false);
        var mirrored = Run(swapped: true);

        Assert.Equal(BattleOutcome.PlayerVictory, normal.Outcome);
        Assert.Equal(BattleOutcome.OpponentVictory, mirrored.Outcome);
        Assert.Equal(normal.DurationMilliseconds, mirrored.DurationMilliseconds);
    }

    /// <summary>
    /// Damage is calculated from the pre-batch state, so two attackers cannot double-spend a kill.
    /// </summary>
    /// <remarks>
    /// Both attackers report the HP after their own blow, applied in turn — the second one is not
    /// told the target still had full health, and it is not cancelled for having arrived second.
    /// </remarks>
    [Fact]
    public void Two_attackers_at_one_timestamp_both_take_HP_off_the_same_target()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("first", new Hex(3, 3), Fight.Stats(power: 4))
            .Deploy("second", new Hex(3, 2), Fight.Stats(power: 4));

        var opponent = new ArmyUnderTest("opponent").Deploy("foe", new Hex(4, 3), Fight.Stats(hp: 1_000));

        Assert.Equal(1, new Hex(3, 3).DistanceTo(new Hex(4, 3)));
        Assert.Equal(1, new Hex(3, 2).DistanceTo(new Hex(4, 3)));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        var opening = result.EventsOf<AttackResolved>()
            .Where(attack => attack.TimeMilliseconds == 0 && attack.TargetId == result.Id("foe"))
            .ToList();

        Assert.Equal(2, opening.Count);
        Assert.Equal([980, 960], opening.Select(attack => attack.TargetHpAfter));
    }
}
