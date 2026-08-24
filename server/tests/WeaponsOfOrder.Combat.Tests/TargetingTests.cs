using Xunit;

namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// Who a Unit attacks: closest, then least armoured, then the roster's own order.
/// </summary>
/// <remarks>
/// Each test reads the very first attack of a battle, which is decided from the deployment the
/// test wrote down and nothing else.
/// </remarks>
public class TargetingTests
{
    /// <summary>The target of the first attack <paramref name="reference"/> made.</summary>
    private static string FirstTargetOf(BattleResult result, string reference)
    {
        var first = result.AttacksBy(reference).First();

        return result.Combatants.Single(combatant => combatant.Id == first.TargetId).Reference;
    }

    [Fact]
    public void A_Unit_attacks_the_closest_enemy()
    {
        var player = new ArmyUnderTest("player").Deploy("blade", new Hex(3, 3), Fight.Stats());

        // Adjacent, and one hex further back. Nothing else separates them.
        var opponent = new ArmyUnderTest("opponent")
            .Deploy("far", new Hex(5, 3), Fight.Stats(hp: 10_000))
            .Deploy("near", new Hex(4, 3), Fight.Stats(hp: 10_000));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        Assert.Equal("near", FirstTargetOf(result, "blade"));
    }

    /// <summary>Distance beats armour: a heavily armoured enemy standing closer is the target.</summary>
    [Fact]
    public void Distance_outranks_the_preference_for_softer_targets()
    {
        var player = new ArmyUnderTest("player").Deploy("blade", new Hex(3, 3), Fight.Stats());

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("armoured", new Hex(4, 3), Fight.Stats(hp: 10_000, defense: 80))
            .Deploy("soft", new Hex(5, 3), Fight.Stats(hp: 10_000, defense: 0));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        Assert.Equal("armoured", FirstTargetOf(result, "blade"));
    }

    [Fact]
    public void Equally_close_enemies_are_split_by_the_lower_Defense()
    {
        var player = new ArmyUnderTest("player").Deploy("blade", new Hex(3, 3), Fight.Stats());

        // Both adjacent to (3,3). Only their Defense differs, and the harder one is listed first so
        // the answer cannot come from roster order by accident.
        var opponent = new ArmyUnderTest("opponent")
            .Deploy("armoured", new Hex(4, 3), Fight.Stats(hp: 10_000, defense: 60))
            .Deploy("soft", new Hex(4, 4), Fight.Stats(hp: 10_000, defense: 5));

        Assert.Equal(1, new Hex(3, 3).DistanceTo(new Hex(4, 3)));
        Assert.Equal(1, new Hex(3, 3).DistanceTo(new Hex(4, 4)));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        Assert.Equal("soft", FirstTargetOf(result, "blade"));
    }

    /// <summary>
    /// An exact tie resolves by the roster's stable order, and resolves the same way every time.
    /// </summary>
    /// <remarks>
    /// Canon leaves this deliberately unauthored and says deterministic implementation ordering is
    /// sufficient. What matters is that it is the <em>same</em> answer on every run, which is what
    /// the repetition below is for.
    /// </remarks>
    [Fact]
    public void An_exact_tie_resolves_stably()
    {
        static string Run()
        {
            var player = new ArmyUnderTest("player").Deploy("blade", new Hex(3, 3), Fight.Stats());

            var opponent = new ArmyUnderTest("opponent")
                .Deploy("first", new Hex(4, 3), Fight.Stats(hp: 10_000, defense: 20))
                .Deploy("second", new Hex(4, 4), Fight.Stats(hp: 10_000, defense: 20));

            return FirstTargetOf(
                BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick())),
                "blade");
        }

        Assert.Equal(["first"], Enumerable.Range(0, 5).Select(_ => Run()).Distinct());
    }

    /// <summary>
    /// A melee Unit skips an enemy it has no reachable attack hex beside.
    /// </summary>
    /// <remarks>
    /// The sealed enemy is the softest of the equally closest, so a targeter that ignored
    /// reachability would choose it. Every hex touching it is taken — two by its own allies and one
    /// by a player Unit already fighting it — so the blade takes the guard it can actually get to.
    /// <para>
    /// This is the melee-jail rule read from the attacker's side: an unreachable enemy is not a
    /// target, it is a distraction.
    /// </para>
    /// </remarks>
    [Fact]
    public void A_melee_Unit_skips_an_enemy_it_cannot_reach()
    {
        var sealedHex = new Hex(4, 0);
        var onBoard = sealedHex.Neighbours().Where(hex => hex.IsOn(Battlefield.Canonical)).ToList();

        // The corner is what makes the seal possible at all: three neighbours, not six.
        Assert.Equal([new Hex(5, 0), new Hex(4, 1), new Hex(3, 0)], onBoard);

        var player = new ArmyUnderTest("player")
            .Deploy("blade", new Hex(0, 0), Fight.Stats(hp: 10_000, power: 1))

            // Takes the one hex beside the sealed enemy that lies in the player's own half, and
            // never moves off it because it is already in melee with what it is sealing in.
            .Deploy("jailer", new Hex(3, 0), Fight.Stats(hp: 10_000, power: 1));

        var opponent = new ArmyUnderTest("opponent")
            .Deploy("sealed", sealedHex, Fight.Stats(hp: 10_000, power: 1, defense: 0))
            // Reach enough to fight the jailer from where they stand, so the seal never opens by
            // one of them wandering off to close the distance.
            .Deploy("guard-side", new Hex(5, 0), Fight.Stats(hp: 10_000, power: 1, defense: 50, range: 5))
            .Deploy("guard-front", new Hex(4, 1), Fight.Stats(hp: 10_000, power: 1, defense: 50, range: 5));

        Assert.Equal(4, new Hex(0, 0).DistanceTo(sealedHex));
        Assert.Equal(4, new Hex(0, 0).DistanceTo(new Hex(4, 1)));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick(maximumSeconds: 6)));

        Assert.NotEmpty(result.AttacksBy("blade"));
        Assert.Equal("guard-front", FirstTargetOf(result, "blade"));

        var sealedId = result.Id("sealed");
        Assert.DoesNotContain(result.AttacksBy("blade"), attack => attack.TargetId == sealedId);
    }

    /// <summary>A Unit's reach comes from its weapon's Range and from nothing else it is called.</summary>
    [Fact]
    public void Reach_is_the_configured_Range()
    {
        var player = new ArmyUnderTest("player").Deploy("archer", new Hex(3, 3), Fight.Stats(range: 3));
        var opponent = new ArmyUnderTest("opponent").Deploy("mark", new Hex(6, 3), Fight.Stats(hp: 10_000));

        Assert.Equal(3, new Hex(3, 3).DistanceTo(new Hex(6, 3)));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        // In reach from where it stands, so it opens fire immediately and takes no step to do it.
        Assert.Equal(0, result.AttacksBy("archer").First().TimeMilliseconds);
        Assert.DoesNotContain(result.MovesBy("archer"), move => move.TimeMilliseconds == 0);
    }

    /// <summary>A ranged Unit shoots over the ally standing between it and its target.</summary>
    /// <remarks>
    /// Canon says ranged Units do not need a free hex beside the enemy and may attack over
    /// frontliners. There is deliberately no line of sight in this game.
    /// </remarks>
    [Fact]
    public void A_ranged_Unit_shoots_over_the_Unit_in_front_of_it()
    {
        var player = new ArmyUnderTest("player")
            .Deploy("archer", new Hex(2, 3), Fight.Stats(range: 3))
            .Deploy("frontliner", new Hex(3, 3), Fight.Stats(hp: 10_000));

        var opponent = new ArmyUnderTest("opponent").Deploy("mark", new Hex(4, 3), Fight.Stats(hp: 10_000));

        Assert.Equal(2, new Hex(2, 3).DistanceTo(new Hex(4, 3)));

        var result = BattleSimulator.Simulate(Fight.Between(player, opponent, Fight.Quick()));

        Assert.Equal(0, result.AttacksBy("archer").First().TimeMilliseconds);
        Assert.Equal("mark", FirstTargetOf(result, "archer"));
    }
}
