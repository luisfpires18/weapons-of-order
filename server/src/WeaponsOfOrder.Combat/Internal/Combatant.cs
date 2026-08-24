namespace WeaponsOfOrder.Combat.Internal;

/// <summary>Where a combatant currently is, from the simulation's point of view.</summary>
internal enum CombatantState
{
    /// <summary>Alive, waiting off-board for an active slot and a free entry hex.</summary>
    Reserve = 0,

    /// <summary>Alive, on the battlefield.</summary>
    Active = 1,

    Dead = 2,
}

/// <summary>
/// One Unit's mutable state for the length of one battle.
/// </summary>
/// <remarks>
/// Internal and mutable on purpose. The boundary this project promises to be immutable is
/// <see cref="BattleInput"/> in and <see cref="BattleResult"/> out; inside one
/// <see cref="BattleSimulator"/> call, a battle is a state machine, and pretending otherwise
/// would mean rebuilding sixteen records every fiftieth of a second to no one's benefit.
/// </remarks>
internal sealed class Combatant
{
    public required CombatantId Id { get; init; }

    public required BattleSide Side { get; init; }

    public required string Reference { get; init; }

    public required string Name { get; init; }

    public required CombatantStats Stats { get; init; }

    /// <summary>Queue position among its army's reserves, or null if it started on the board.</summary>
    public int? ReserveOrder { get; init; }

    /// <summary>The rear-column hex this reserve must enter through, or null if it started on the board.</summary>
    public Hex? ReserveEntryHex { get; init; }

    public required int AttackIntervalMilliseconds { get; init; }

    public required int MovementIntervalMilliseconds { get; init; }

    public CombatantState State { get; set; }

    /// <summary>Where it is, or where it fell. Null only while it has never reached the board.</summary>
    public Hex? Position { get; set; }

    public int Hp { get; set; }

    public int Energy { get; set; }

    /// <summary>The earliest time it may attack again.</summary>
    public int AttackReadyAt { get; set; }

    /// <summary>The earliest time it may take another step.</summary>
    public int MoveReadyAt { get; set; }

    /// <summary>How many attacks it has made, which is what makes each critical roll its own.</summary>
    public int AttacksMade { get; set; }

    /// <summary>When this reserve next attempts to enter, or null when no attempt is pending.</summary>
    public int? ReserveAttemptAt { get; set; }

    public bool Alive => State != CombatantState.Dead;

    public bool OnBoard => State == CombatantState.Active;
}
