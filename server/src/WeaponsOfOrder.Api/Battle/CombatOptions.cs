using WeaponsOfOrder.Combat;
using WeaponsOfOrder.Infrastructure.Gameplay;

namespace WeaponsOfOrder.Api.Battle;

/// <summary>
/// Every tunable number the battle uses that is not a Unit's or a weapon's own, bound from the
/// <c>Combat</c> configuration section.
/// </summary>
/// <remarks>
/// <strong>These are temporary prototype balance values, not canon.</strong> The combat canon
/// fixes the structure — one Power stat, the Defense curve's shape, one Energy bar, a Heavy
/// attack at full Energy, two finite termination guards, 8 active and 16 total — and says in as
/// many words that the numbers are configuration. They live here, and are mirrored in
/// <c>appsettings.json</c>, so tuning a battle is an edit to data rather than a hunt through the
/// simulator.
/// </remarks>
internal sealed class CombatOptions
{
    public const string SectionName = "Combat";

    public CombatTuningSettings Tuning { get; set; } = new();

    /// <summary>What each weapon weight is worth in Attack Interval seconds.</summary>
    public WeaponWeightSettings WeightIntervalSeconds { get; set; } = new();

    /// <summary>What a Unit's Mounted state is worth as a Movement Speed.</summary>
    public MovementSpeedSettings MovementSpeed { get; set; } = new();

    public UnarmedSettings Unarmed { get; set; } = new();

    public TrainingOpponentSettings TrainingOpponent { get; set; } = new();

    /// <summary>The settings above as the simulator's own tuning record.</summary>
    public CombatTuning ToTuning() => Tuning.ToTuning();

    /// <summary>
    /// The Movement Speed a Unit in this state fights with.
    /// </summary>
    /// <remarks>
    /// The whole of the Mounted-to-Movement-Speed translation, and the only place it happens. The
    /// simulator is handed the number that comes out of here and never learns what produced it,
    /// which is what lets the creator later author speed some other way — per Unit, per tier, per
    /// anything — by changing this method and nothing below it.
    /// <para>
    /// Canon's one inherent movement distinction for v1 is that Mounted Units are slightly faster.
    /// There are deliberately no other tiers and no equipment movement modifiers.
    /// </para>
    /// </remarks>
    public double MovementSpeedFor(bool mounted)
        => mounted ? MovementSpeed.Mounted : MovementSpeed.Foot;

    /// <summary>The Attack Interval a weapon of this weight adds. Negative makes the loadout faster.</summary>
    public double IntervalFor(WeaponWeight weight) => weight switch
    {
        WeaponWeight.Light => WeightIntervalSeconds.Light,
        WeaponWeight.Medium => WeightIntervalSeconds.Medium,
        WeaponWeight.Heavy => WeightIntervalSeconds.Heavy,
        _ => 0,
    };
}

/// <summary>The simulator's tuning, in a shape the configuration binder can fill.</summary>
/// <remarks>
/// A separate class from <see cref="CombatTuning"/> because that one is an immutable record with
/// <c>init</c> properties and this one has to be a mutable bag the binder writes into. The
/// defaults are kept in step with it, and <see cref="ToTuning"/> is the only crossing point.
/// </remarks>
internal sealed class CombatTuningSettings
{
    public double PowerScale { get; set; } = CombatTuning.Default.PowerScale;

    public double DefenseConstant { get; set; } = CombatTuning.Default.DefenseConstant;

    public double NormalCoefficient { get; set; } = CombatTuning.Default.NormalCoefficient;

    public double HeavyCoefficient { get; set; } = CombatTuning.Default.HeavyCoefficient;

    public double CriticalMultiplier { get; set; } = CombatTuning.Default.CriticalMultiplier;

    public int MinimumDamage { get; set; } = CombatTuning.Default.MinimumDamage;

    public int MaximumEnergy { get; set; } = CombatTuning.Default.MaximumEnergy;

    public int EnergyPerAttack { get; set; } = CombatTuning.Default.EnergyPerAttack;

    public int TickMilliseconds { get; set; } = CombatTuning.Default.TickMilliseconds;

    public double BaseMovementSecondsPerHex { get; set; } = CombatTuning.Default.BaseMovementSecondsPerHex;

    public double MinimumAttackIntervalSeconds { get; set; } = CombatTuning.Default.MinimumAttackIntervalSeconds;

    public int ActiveLimit { get; set; } = CombatTuning.Default.ActiveLimit;

    public int ReserveLimit { get; set; } = CombatTuning.Default.ReserveLimit;

    public int ArmyLimit { get; set; } = CombatTuning.Default.ArmyLimit;

    public double ReserveEntryDelaySeconds { get; set; } = CombatTuning.Default.ReserveEntryDelaySeconds;

    public double MaximumDurationSeconds { get; set; } = CombatTuning.Default.MaximumDurationSeconds;

    public double NoProgressSeconds { get; set; } = CombatTuning.Default.NoProgressSeconds;

    public CombatTuning ToTuning() => new()
    {
        PowerScale = PowerScale,
        DefenseConstant = DefenseConstant,
        NormalCoefficient = NormalCoefficient,
        HeavyCoefficient = HeavyCoefficient,
        CriticalMultiplier = CriticalMultiplier,
        MinimumDamage = MinimumDamage,
        MaximumEnergy = MaximumEnergy,
        EnergyPerAttack = EnergyPerAttack,
        TickMilliseconds = TickMilliseconds,
        BaseMovementSecondsPerHex = BaseMovementSecondsPerHex,
        MinimumAttackIntervalSeconds = MinimumAttackIntervalSeconds,
        ActiveLimit = ActiveLimit,
        ReserveLimit = ReserveLimit,
        ArmyLimit = ArmyLimit,
        ReserveEntryDelaySeconds = ReserveEntryDelaySeconds,
        MaximumDurationSeconds = MaximumDurationSeconds,
        NoProgressSeconds = NoProgressSeconds,
    };
}

/// <summary>
/// What each weapon weight does to a loadout's Attack Interval, in seconds.
/// </summary>
/// <remarks>
/// <strong>Temporary prototype values, not canon.</strong> The weapon registry says weight
/// participates in the Attack Interval calculation and leaves the exact modifiers as balance
/// work. Every equipped weapon contributes its own, which is what lets the registry's worked
/// example — Light armour and two Light swords at roughly a second between alternating attacks —
/// fall out of the arithmetic rather than being special-cased.
/// </remarks>
internal sealed class WeaponWeightSettings
{
    public double Light { get; set; } = -0.2;

    public double Medium { get; set; }

    public double Heavy { get; set; } = 0.3;
}

/// <summary>
/// The Movement Speed each Unit state resolves to.
/// </summary>
/// <remarks>
/// <strong>Temporary prototype values, not canon.</strong> Canon says a Mounted Unit is slightly
/// faster than one on foot and leaves the amount as tuning.
/// <para>
/// A multiple of standard movement rather than a duration, because that is the sense canon gives
/// the stat: higher is faster. The seconds a hex takes are
/// <c>Combat:Tuning:BaseMovementSecondsPerHex</c> divided by it.
/// </para>
/// </remarks>
internal sealed class MovementSpeedSettings
{
    /// <summary>Standard movement. The scale everything else is a multiple of.</summary>
    public double Foot { get; set; } = 1.0;

    /// <summary>Above <see cref="Foot"/>, because canon says Mounted is faster.</summary>
    public double Mounted { get; set; } = 1.4;
}

/// <summary>
/// What a Unit holding nothing fights with.
/// </summary>
/// <remarks>
/// <strong>Prototype implementation detail, not canon.</strong> Canon does not say what an
/// empty-handed Unit does, and it does not need to: a Unit with no weapon adds no weapon Power
/// and reaches one hex, which is the smallest answer that keeps a battle legal without inventing
/// an unarmed class ability. The damage floor means it still chips rather than being harmless.
/// </remarks>
internal sealed class UnarmedSettings
{
    public int Power { get; set; }

    public double CriticalChance { get; set; }

    public int Range { get; set; } = 1;

    public string Weight { get; set; } = nameof(WeaponWeight.Medium);
}

/// <summary>
/// The opposition the creator presses Battle against.
/// </summary>
/// <remarks>
/// <strong>A temporary battle harness, not game content.</strong> There is no recruitment, no PvP
/// and no authored enemy roster yet, and this exists only so the engine can be watched running.
/// It is deliberately configuration rather than Unit content: nothing here is in the Unit
/// registry, nothing is persisted, and the names are neutral placeholders — no kingdom, no
/// faction, no character, no class.
/// <para>
/// When a real opponent exists — a recruited defensive army, a matched player — it replaces this
/// section and nothing else about the battle changes.
/// </para>
/// </remarks>
internal sealed class TrainingOpponentSettings
{
    public string Name { get; set; } = "Training Opposition";

    public List<TrainingCombatantSettings> Active { get; set; } = [];

    /// <summary>Reserves, in queue order.</summary>
    public List<TrainingCombatantSettings> Reserves { get; set; } = [];
}

/// <summary>One combatant in the training opposition, stats and all.</summary>
/// <remarks>
/// Final stats rather than a Unit and a loadout, because it is not a Unit and has no loadout.
/// Giving it one would mean inventing a Unit definition and putting a forged weapon in an
/// account nobody owns.
/// </remarks>
internal sealed class TrainingCombatantSettings
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex column. Ignored for a reserve, which enters through its assigned rear hex.</summary>
    public int Column { get; set; }

    public int Row { get; set; }

    public int Hp { get; set; }

    public int Power { get; set; }

    public int Defense { get; set; }

    public double AttackIntervalSeconds { get; set; }

    public double CriticalChance { get; set; }

    public int Range { get; set; } = 1;

    /// <summary>
    /// Whether this combatant is mounted, translated through the same configured mapping a Unit's
    /// is.
    /// </summary>
    /// <remarks>
    /// Authored as the state rather than as a speed so that retuning what Mounted is worth moves
    /// the opposition with the player's own Units. It is still translated here, in the API, and
    /// the simulator receives only the number.
    /// </remarks>
    public bool Mounted { get; set; }

    public CombatantStats ToStats(double movementSpeed) => new()
    {
        Hp = Hp,
        Power = Power,
        Defense = Defense,
        AttackIntervalSeconds = AttackIntervalSeconds,
        CriticalChance = CriticalChance,
        Range = Range,
        MovementSpeed = movementSpeed,
    };
}
