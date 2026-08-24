namespace WeaponsOfOrder.Combat;

/// <summary>Who won.</summary>
public enum BattleOutcome
{
    Draw = 0,
    PlayerVictory = 1,
    OpponentVictory = 2,
}

/// <summary>Why the battle stopped.</summary>
public enum BattleEndReason
{
    /// <summary>One army had every active Unit and every reserve dead.</summary>
    Elimination = 0,

    /// <summary>Both armies were fully eliminated by the same timestamp's batch.</summary>
    MutualElimination = 1,

    /// <summary>The hard cap on simulated combat time expired.</summary>
    MaximumDuration = 2,

    /// <summary>Combat ran for the no-progress window without an HP change, a death or a reserve entering.</summary>
    NoProgress = 3,
}

/// <summary>
/// A combatant's stable identity within one battle.
/// </summary>
/// <remarks>
/// Assigned from the input's own order — active Units first, then reserves, per side — so the
/// same input always produces the same identifiers. It is the simulator's last-resort
/// deterministic tie-break, and canon explicitly permits that: stable implementation ordering
/// is sufficient where distance and Defense are exactly equal.
/// </remarks>
public readonly record struct CombatantId(BattleSide Side, int Index)
{
    public override string ToString() => $"{(Side == BattleSide.Player ? "P" : "O")}{Index}";
}

/// <summary>Where a combatant was when the battle ended.</summary>
public enum CombatantEndState
{
    /// <summary>Alive on the battlefield.</summary>
    Active = 0,

    /// <summary>Alive, waiting off-board. A guard Draw does not reinterpret this as dead.</summary>
    Reserve = 1,

    Dead = 2,
}

/// <summary>
/// One combatant, as the battle knew it.
/// </summary>
/// <remarks>
/// The roster a playback client draws from: identity, the stats it fought with, and where it
/// finished. Everything that happened to it in between is in the event log.
/// </remarks>
public sealed record BattleCombatant
{
    public required CombatantId Id { get; init; }

    public required BattleSide Side { get; init; }

    public required string Reference { get; init; }

    public required string Name { get; init; }

    public required CombatantStats Stats { get; init; }

    /// <summary>Queue position for a Unit that started in reserve, otherwise null.</summary>
    public int? ReserveOrder { get; init; }

    /// <summary>The rear-column hex a reserve was assigned to enter through, otherwise null.</summary>
    public Hex? ReserveEntryHex { get; init; }

    public required CombatantEndState EndState { get; init; }

    public required int FinalHp { get; init; }

    public required int FinalEnergy { get; init; }

    /// <summary>Where it finished, or null if it never reached the battlefield.</summary>
    public Hex? FinalHex { get; init; }
}

/// <summary>
/// The authoritative result of one battle.
/// </summary>
/// <remarks>
/// Complete: a client can draw the whole fight from <see cref="Events"/> and
/// <see cref="Combatants"/> without recomputing a single rule. Canon's MVP explicitly allows
/// returning the full log at once, and progressive delivery can be added later without this
/// shape moving.
/// </remarks>
public sealed record BattleResult
{
    public required BattleOutcome Outcome { get; init; }

    public required BattleEndReason Reason { get; init; }

    /// <summary>How long the battle took on the simulated combat clock.</summary>
    public required int DurationMilliseconds { get; init; }

    public required long Seed { get; init; }

    public required Battlefield Battlefield { get; init; }

    public required IReadOnlyList<BattleCombatant> Combatants { get; init; }

    /// <summary>
    /// Everything that happened, in order, each stamped with the simulated time it happened at.
    /// </summary>
    /// <remarks>
    /// Events sharing a timestamp happened at the same simulation moment and must be presented
    /// as one. That is not a rendering nicety: it is the difference between a mutual last kill
    /// reading as a Draw and reading as somebody winning by a frame.
    /// </remarks>
    public required IReadOnlyList<BattleEvent> Events { get; init; }
}
