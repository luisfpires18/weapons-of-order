namespace WeaponsOfOrder.Combat.Tests;

/// <summary>
/// Builds the battles these tests fight.
/// </summary>
/// <remarks>
/// Every helper here is a plain function over the public boundary. Nothing reaches inside the
/// simulator, because a test that needs to would be testing the implementation rather than the
/// rule — and the rules are what canon fixes.
/// </remarks>
internal static class Fight
{
    /// <summary>
    /// A combatant's stats, with defaults chosen to be uninteresting.
    /// </summary>
    /// <remarks>
    /// A test names only what it is about. Everything unnamed is deliberately plain: melee reach,
    /// no Defense, no Critical Chance, one attack a second, standard movement.
    /// <para>
    /// <paramref name="movementSpeed"/> is the canonical stat — a multiple of standard movement,
    /// where higher is faster. There is no Mounted here, and no way to express one: what makes a
    /// Unit quick was resolved before the battle was built.
    /// </para>
    /// </remarks>
    public static CombatantStats Stats(
        int hp = 100,
        int power = 10,
        int defense = 0,
        double interval = 1.0,
        double crit = 0,
        int range = 1,
        double movementSpeed = 1.0)
        => new()
        {
            Hp = hp,
            Power = power,
            Defense = defense,
            AttackIntervalSeconds = interval,
            CriticalChance = crit,
            Range = range,
            MovementSpeed = movementSpeed,
        };

    /// <summary>Tuning with guards short enough that a stalemate test finishes in milliseconds.</summary>
    public static CombatTuning Quick(double maximumSeconds = 10, double noProgressSeconds = 5)
        => CombatTuning.Default with
        {
            MaximumDurationSeconds = maximumSeconds,
            NoProgressSeconds = noProgressSeconds,
        };

    public static BattleInput Between(
        ArmyUnderTest player,
        ArmyUnderTest opponent,
        CombatTuning? tuning = null,
        Battlefield? field = null,
        long seed = 20260823)
        => new()
        {
            Seed = seed,
            Player = player.Build(),
            Opponent = opponent.Build(),
            Battlefield = field ?? Battlefield.Canonical,
            Tuning = tuning ?? CombatTuning.Default,
        };

    /// <summary>The identifier the battle gave the combatant this test called <paramref name="reference"/>.</summary>
    public static CombatantId Id(this BattleResult result, string reference)
        => result.Combatants.Single(combatant => combatant.Reference == reference).Id;

    public static BattleCombatant Combatant(this BattleResult result, string reference)
        => result.Combatants.Single(combatant => combatant.Reference == reference);

    public static IEnumerable<T> EventsOf<T>(this BattleResult result)
        where T : BattleEvent
        => result.Events.OfType<T>();

    public static IEnumerable<AttackResolved> AttacksBy(this BattleResult result, string reference)
    {
        var id = result.Id(reference);

        return result.EventsOf<AttackResolved>().Where(attack => attack.AttackerId == id);
    }

    public static IEnumerable<CombatantMoved> MovesBy(this BattleResult result, string reference)
    {
        var id = result.Id(reference);

        return result.EventsOf<CombatantMoved>().Where(move => move.Id == id);
    }

    /// <summary>
    /// The whole battle written out as text, one line per event.
    /// </summary>
    /// <remarks>
    /// What the determinism tests compare. Records do not compare their lists structurally, and
    /// a line-by-line difference says which moment diverged rather than only that something did.
    /// </remarks>
    public static IReadOnlyList<string> Transcript(this BattleResult result)
        =>
        [
            $"outcome {result.Outcome} {result.Reason} at {result.DurationMilliseconds}",
            .. result.Events.Select(Describe),
            .. result.Combatants.Select(combatant =>
                $"{combatant.Id} {combatant.EndState} hp={combatant.FinalHp} energy={combatant.FinalEnergy} "
                + $"at={combatant.FinalHex}"),
        ];

    private static string Describe(BattleEvent moment) => moment switch
    {
        CombatantDeployed deployed => $"{deployed.TimeMilliseconds} deploy {deployed.Id} {deployed.Hex}",
        ReserveEntered entered => $"{entered.TimeMilliseconds} enter {entered.Id} {entered.Hex}",
        CombatantMoved moved => $"{moved.TimeMilliseconds} move {moved.Id} {moved.From}->{moved.To}",
        AttackResolved attack =>
            $"{attack.TimeMilliseconds} attack {attack.AttackerId}->{attack.TargetId} {attack.Kind} "
            + $"crit={attack.Critical} damage={attack.Damage} hp={attack.TargetHpAfter} "
            + $"energy={attack.AttackerEnergyAfter}",
        CombatantDied died => $"{died.TimeMilliseconds} died {died.Id} {died.Hex}",
        BattleEnded ended => $"{ended.TimeMilliseconds} end {ended.Outcome} {ended.Reason}",
        _ => throw new InvalidOperationException($"Unhandled event {moment.GetType().Name}."),
    };
}

/// <summary>One army being assembled for a test.</summary>
internal sealed class ArmyUnderTest(string name)
{
    private readonly List<DeployedCombatantInput> _active = [];
    private readonly List<BattleCombatantInput> _reserves = [];

    /// <summary>A Unit starting on the battlefield, named so a test can find it in the result.</summary>
    public ArmyUnderTest Deploy(string reference, Hex hex, CombatantStats stats)
    {
        _active.Add(new DeployedCombatantInput(new BattleCombatantInput(reference, reference, stats), hex));

        return this;
    }

    /// <summary>A Unit waiting off-board. Call order is queue order, as it is for a player.</summary>
    public ArmyUnderTest Reserve(string reference, CombatantStats stats)
    {
        _reserves.Add(new BattleCombatantInput(reference, reference, stats));

        return this;
    }

    public BattleArmyInput Build() => new(name, _active, _reserves);
}
