namespace WeaponsOfOrder.Combat;

/// <summary>
/// The final combat stats one combatant fights with.
/// </summary>
/// <remarks>
/// Already totalled. Canon builds a final stat as <c>Unit base + weapon + armour</c>, and doing
/// that addition is the caller's job: this project has no idea what a weapon is, which is what
/// keeps it from ever needing to know what a Rune or a piece of armour is either.
/// <para>
/// The six stats are exactly canon's universal set. There is no Dodge, Block, Critical Damage,
/// Armor Penetration, Attack Power or Special Power here, because none of those are stats in
/// this game.
/// </para>
/// </remarks>
public sealed record CombatantStats
{
    public required int Hp { get; init; }

    /// <summary>The single offensive scaling stat.</summary>
    public required int Power { get; init; }

    /// <summary>The single mitigation stat, applied through the canon's diminishing curve.</summary>
    public required int Defense { get; init; }

    /// <summary>Seconds between auto attacks. Lower is faster.</summary>
    public required double AttackIntervalSeconds { get; init; }

    /// <summary>Chance for an attack to crit, from 0 to 1.</summary>
    public required double CriticalChance { get; init; }

    /// <summary>
    /// Attack range in hexes: 1 for an ordinary melee weapon, more for a weapon whose content
    /// authors it.
    /// </summary>
    /// <remarks>
    /// Comes from the weapon, never from what a Unit is called. There are no weapon proficiency
    /// restrictions in this game and no Unit is inherently ranged; a Unit shoots because it is
    /// holding something that reaches.
    /// </remarks>
    public required int Range { get; init; }

    /// <summary>
    /// Whether the Unit is Mounted, which is the one inherent movement distinction v1 has.
    /// </summary>
    public bool Mounted { get; init; }
}

/// <summary>
/// One Unit an army brings to a battle.
/// </summary>
/// <param name="Reference">
/// The caller's own identifier, carried through untouched so it can match a combatant in the
/// result back to whatever it came from. The simulator never interprets it.
/// </param>
/// <param name="Name">Display copy for the result. Nothing reads it.</param>
public sealed record BattleCombatantInput(string Reference, string Name, CombatantStats Stats);

/// <summary>A Unit starting the battle on the board, at the hex the player put it on.</summary>
public sealed record DeployedCombatantInput(BattleCombatantInput Combatant, Hex Hex);

/// <summary>
/// One army: what starts on the battlefield, and what waits behind it in order.
/// </summary>
/// <remarks>
/// The reserve list's order is the queue order. It is the player's pre-battle decision and the
/// simulator preserves it exactly.
/// </remarks>
public sealed record BattleArmyInput(
    string Name,
    IReadOnlyList<DeployedCombatantInput> Active,
    IReadOnlyList<BattleCombatantInput> Reserves);

/// <summary>
/// Everything a battle is resolved from.
/// </summary>
/// <remarks>
/// This is the whole boundary. The same <see cref="BattleInput"/> always produces the same
/// <see cref="BattleResult"/>, event for event, because there is nothing else the simulation
/// can read: no clock, no ambient randomness, no database, no request. <see cref="Seed"/> is
/// the only source of chance and the server owns it.
/// </remarks>
public sealed record BattleInput
{
    public required long Seed { get; init; }

    public required BattleArmyInput Player { get; init; }

    public required BattleArmyInput Opponent { get; init; }

    public Battlefield Battlefield { get; init; } = Battlefield.Canonical;

    public CombatTuning Tuning { get; init; } = CombatTuning.Default;

    /// <summary>The army on a given side.</summary>
    public BattleArmyInput Army(BattleSide side) => side == BattleSide.Player ? Player : Opponent;
}

/// <summary>An input the simulator refuses to run, because the battle it describes is not legal.</summary>
/// <remarks>
/// Thrown rather than reported as an outcome. A malformed army is a caller's bug — the API
/// validates a player's deployment long before it reaches here — and a battle resolved from it
/// would be authoritative nonsense.
/// </remarks>
public sealed class InvalidBattleInputException(string message) : Exception(message);
