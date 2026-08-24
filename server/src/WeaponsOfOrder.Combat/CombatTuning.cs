namespace WeaponsOfOrder.Combat;

/// <summary>
/// Every tunable number the simulation uses.
/// </summary>
/// <remarks>
/// <strong>These are v1 balance values and prototype implementation details, not canon.</strong>
/// The combat canon fixes the structure — one Power stat, the Defense curve's shape, one Energy
/// bar, a Heavy attack at full Energy, two finite termination guards — and says in as many
/// words that the numbers are configuration. They live in one record so tuning the battle is an
/// edit to data rather than a hunt through the simulator.
/// <para>
/// The simulator never reads a value that is not on this record or on the input. There is no
/// constant buried in a formula.
/// </para>
/// </remarks>
public sealed record CombatTuning
{
    /// <summary>The values the game currently runs with, mirrored in the API's configuration.</summary>
    public static readonly CombatTuning Default = new();

    /// <summary>Raw auto-attack damage per point of final Power. Canon's current baseline is 5.</summary>
    public double PowerScale { get; init; } = 5;

    /// <summary>
    /// The denominator in <c>Defense / (Defense + K)</c>. Canon's current baseline is 100, which
    /// puts 100 Defense at half damage taken.
    /// </summary>
    public double DefenseConstant { get; init; } = 100;

    /// <summary>Damage multiplier for an ordinary auto attack.</summary>
    public double NormalCoefficient { get; init; } = 1.0;

    /// <summary>Damage multiplier for a Heavy attack. Canon's current baseline is 2.5x.</summary>
    public double HeavyCoefficient { get; init; } = 2.5;

    /// <summary>Damage multiplier for a critical hit. Canon's current baseline is 2x.</summary>
    public double CriticalMultiplier { get; init; } = 2.0;

    /// <summary>The least damage a hit that lands may deal.</summary>
    public int MinimumDamage { get; init; } = 1;

    /// <summary>The top of the single Energy bar.</summary>
    public int MaximumEnergy { get; init; } = 100;

    /// <summary>Energy granted by a successful normal auto attack. Does not overflow.</summary>
    public int EnergyPerAttack { get; init; } = 10;

    /// <summary>
    /// How often the simulated clock advances, in milliseconds.
    /// </summary>
    /// <remarks>
    /// <strong>Prototype implementation detail, not canon.</strong> Canon requires an
    /// authoritative combat clock on which same-timestamp attacks resolve as one batch; it does
    /// not say whether that clock is continuous or stepped. A fixed step in whole milliseconds
    /// is the smallest thing that makes "the same timestamp" an exact answer rather than a
    /// floating-point comparison, and it is what makes the simultaneity rule testable.
    /// <para>
    /// Attack and movement intervals are therefore effectively rounded up to this grid.
    /// </para>
    /// </remarks>
    public int TickMilliseconds { get; init; } = 50;

    /// <summary>
    /// How long one hex takes at a Movement Speed of 1.0, in seconds.
    /// </summary>
    /// <remarks>
    /// The scale a combatant's <see cref="CombatantStats.MovementSpeed"/> is a multiple of. There
    /// is one of these rather than one per kind of Unit: which Units are faster is the caller's
    /// answer, and encoding a second duration here would be this project holding an opinion about
    /// who is riding a horse.
    /// </remarks>
    public double BaseMovementSecondsPerHex { get; init; } = 0.6;

    /// <summary>
    /// The lowest Attack Interval any loadout can reach, in seconds.
    /// </summary>
    /// <remarks>
    /// Canon says a floor is required to stop pathological stacking and leaves its value
    /// unlocked. This is that floor, at a value chosen to be out of reach of the current
    /// content rather than to express a balance intention.
    /// </remarks>
    public double MinimumAttackIntervalSeconds { get; init; } = 0.4;

    /// <summary>How many Units one army may have on the battlefield at once.</summary>
    /// <remarks>Canon's Deployment Limit. The current v1 value is 8.</remarks>
    public int ActiveLimit { get; init; } = 8;

    /// <summary>How many Units one army may hold in reserve.</summary>
    /// <remarks>
    /// Stated rather than inferred from the other two. The distinction canon makes is structural —
    /// how many may be on the battlefield, and how many the army has in total — and leaving the
    /// reserve capacity as arithmetic between them would mean a mis-set pair silently redefines it.
    /// </remarks>
    public int ReserveLimit { get; init; } = 8;

    /// <summary>How many Units one army may bring to a battle in total, starters plus reserves.</summary>
    /// <remarks>Canon's Army Limit. The current v1 value is 16.</remarks>
    public int ArmyLimit { get; init; } = 16;

    /// <summary>
    /// How long after an active slot opens a reserve attempts to enter, in seconds.
    /// </summary>
    /// <remarks>
    /// Canon requires a short delay and leaves the duration explicitly unresolved. A failed
    /// attempt waits this long again before trying, which is what keeps a permanently blocked
    /// entry hex from spinning.
    /// </remarks>
    public double ReserveEntryDelaySeconds { get; init; } = 2.0;

    /// <summary>
    /// The hard cap on simulated combat time, in seconds. Expiring here is a Draw.
    /// </summary>
    /// <remarks>
    /// Canon locks the existence of this guard and its Draw outcome, and leaves the duration as
    /// balance. It applies even when the no-progress window keeps resetting, so a battle that
    /// cycles forever still ends.
    /// </remarks>
    public double MaximumDurationSeconds { get; init; } = 120;

    /// <summary>
    /// How long combat may run without progress before the battle is a Draw, in seconds.
    /// </summary>
    /// <remarks>
    /// Progress is an HP change, a death, or a reserve successfully entering. Movement,
    /// retargeting, path attempts and failed reserve entries are not progress, which is what
    /// makes a permanent body-block terminate here rather than continue politely forever.
    /// </remarks>
    public double NoProgressSeconds { get; init; } = 15;

    /// <summary>Milliseconds between attacks, for an interval expressed in seconds.</summary>
    public int AttackIntervalMilliseconds(double seconds)
        => ToMilliseconds(Math.Max(seconds, MinimumAttackIntervalSeconds));

    /// <summary>Milliseconds per hex, for a combatant moving at <paramref name="movementSpeed"/>.</summary>
    /// <remarks>
    /// Speed divides the base duration, so a higher number is a shorter step. A speed of zero or
    /// less has no meaning here and is refused when the battle is validated rather than turned
    /// into an infinite or negative interval.
    /// </remarks>
    public int MovementIntervalMilliseconds(double movementSpeed)
        => ToMilliseconds(BaseMovementSecondsPerHex / movementSpeed);

    public int ReserveEntryDelayMilliseconds => ToMilliseconds(ReserveEntryDelaySeconds);

    public int MaximumDurationMilliseconds => ToMilliseconds(MaximumDurationSeconds);

    public int NoProgressMilliseconds => ToMilliseconds(NoProgressSeconds);

    /// <summary>
    /// Seconds as whole milliseconds, never rounding down to nothing.
    /// </summary>
    /// <remarks>
    /// A zero interval would let a Unit act every tick, which is not a fast Unit but a broken
    /// clock. Anything positive is at least one millisecond.
    /// </remarks>
    private static int ToMilliseconds(double seconds)
        => Math.Max(1, (int)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero));
}
