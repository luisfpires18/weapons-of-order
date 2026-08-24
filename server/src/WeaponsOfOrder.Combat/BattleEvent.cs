namespace WeaponsOfOrder.Combat;

/// <summary>Whether an attack was an ordinary auto or the Heavy attack full Energy triggers.</summary>
public enum AttackKind
{
    Normal = 0,

    /// <summary>The 100-Energy attack. At L0 — everything the game currently has — this is the ordinary Heavy.</summary>
    Heavy = 1,
}

/// <summary>
/// One thing that happened at one simulated moment.
/// </summary>
/// <remarks>
/// The log is deliberately small: placement, entry, movement, an attack and its result, a
/// death, the end. There is no scheduling noise, no retarget event and no path chatter, because
/// a playback client has no use for the simulator's internal deliberation — only for what it
/// has to draw.
/// <para>
/// Every event carries the simulated time it happened at. Two events with the same
/// <see cref="TimeMilliseconds"/> are the same moment.
/// </para>
/// </remarks>
public abstract record BattleEvent(int TimeMilliseconds);

/// <summary>A Unit standing on the battlefield as the battle begins.</summary>
public sealed record CombatantDeployed(int TimeMilliseconds, CombatantId Id, Hex Hex)
    : BattleEvent(TimeMilliseconds);

/// <summary>A reserve that reached its assigned entry hex and became active.</summary>
public sealed record ReserveEntered(int TimeMilliseconds, CombatantId Id, Hex Hex)
    : BattleEvent(TimeMilliseconds);

/// <summary>One step to an adjacent hex.</summary>
public sealed record CombatantMoved(int TimeMilliseconds, CombatantId Id, Hex From, Hex To)
    : BattleEvent(TimeMilliseconds);

/// <summary>
/// An attack, and everything it did.
/// </summary>
/// <remarks>
/// Carries the result rather than the ingredients: the client is told the damage, the target's
/// remaining HP and the attacker's remaining Energy, so it never replays the damage pipeline to
/// find out what happened. That is the whole point of the server being authoritative.
/// </remarks>
public sealed record AttackResolved(
    int TimeMilliseconds,
    CombatantId AttackerId,
    CombatantId TargetId,
    AttackKind Kind,
    bool Critical,
    int Damage,
    int TargetHpAfter,
    int AttackerEnergyAfter)
    : BattleEvent(TimeMilliseconds);

/// <summary>A Unit whose HP reached zero, resolved after its whole timestamp batch was applied.</summary>
public sealed record CombatantDied(int TimeMilliseconds, CombatantId Id, Hex Hex)
    : BattleEvent(TimeMilliseconds);

/// <summary>The battle stopping, and why.</summary>
public sealed record BattleEnded(int TimeMilliseconds, BattleOutcome Outcome, BattleEndReason Reason)
    : BattleEvent(TimeMilliseconds);
